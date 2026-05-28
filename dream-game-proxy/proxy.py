from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
import json
import urllib.request
import time
import os
import uuid
from PIL import Image
from io import BytesIO
from secrets import ANTHROPIC_API_KEY, REPLICATE_API_TOKEN

ANTHROPIC_API_KEY = ANTHROPIC_API_KEY
REPLICATE_API_TOKEN = REPLICATE_API_TOKEN

class ProxyHandler(BaseHTTPRequestHandler):

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_cors_headers()
        self.end_headers()

    def do_POST(self):
        content_length = int(self.headers['Content-Length'])
        body = self.rfile.read(content_length)
        data = json.loads(body)
        prompt = data.get('prompt', '')

        if self.path == '/claude':
            result = self.call_claude(prompt)
        elif self.path == '/image':
            result = self.call_replicate(prompt)
        else:
            result = {'error': 'unknown endpoint'}

        response_bytes = json.dumps(result).encode('utf-8')
        self.send_response(200)
        self.send_cors_headers()
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', len(response_bytes))
        self.end_headers()
        self.wfile.write(response_bytes)

    def do_GET(self):
        if self.path == '/health':
            result = {'status': 'Dream Engine proxy is running'}
            response_bytes = json.dumps(result).encode('utf-8')
            self.send_response(200)
            self.send_cors_headers()
            self.send_header('Content-Type', 'application/json')
            self.send_header('Content-Length', len(response_bytes))
            self.end_headers()
            self.wfile.write(response_bytes)

        elif self.path.startswith('/generated/'):
            filename = self.path.replace('/generated/', '')
            filepath = os.path.join('generated', filename)

            with open(filepath, 'rb') as f:
                image_bytes = f.read()

            self.send_response(200)
            self.send_cors_headers()
            self.send_header('Content-Type', 'image/png')
            self.send_header('Content-Length', len(image_bytes))
            self.send_header('Connection', 'close')
            self.send_header('Cache-Control', 'no-cache')
            self.end_headers()
            self.wfile.write(image_bytes)
        else:
            self.send_response(404)
            self.end_headers()

    def send_cors_headers(self):
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')

    def call_claude(self, prompt):
        try:
            payload = json.dumps({
                'model': 'claude-sonnet-4-6',
                'max_tokens': 1024,
                'messages': [{'role': 'user', 'content': prompt}]
            }).encode('utf-8')

            req = urllib.request.Request(
                'https://api.anthropic.com/v1/messages',
                data=payload,
                headers={
                    'x-api-key': ANTHROPIC_API_KEY,
                    'anthropic-version': '2023-06-01',
                    'Content-Type': 'application/json'
                }
            )

            with urllib.request.urlopen(req) as response:
                result = json.loads(response.read())
                text = result['content'][0]['text']
                print("Claude responded successfully")
                return {'text': text}
        except Exception as e:
            print(f"Claude error: {e}")
            return {'text': f'Error: {str(e)}'}

    def call_replicate(self, prompt):
        payload = json.dumps({
            'input': {'prompt': prompt}
        }).encode('utf-8')

        req = urllib.request.Request(
            'https://api.replicate.com/v1/models/stability-ai/stable-diffusion-3/predictions',
            data=payload,
            headers={
                'Authorization': f'Token {REPLICATE_API_TOKEN}',
                'Content-Type': 'application/json'
            }
        )

        with urllib.request.urlopen(req) as response:
            prediction = json.loads(response.read())

        prediction_id = prediction.get('id')
        print(f"Prediction ID: {prediction_id}")

        for _ in range(60):
            time.sleep(3)
            poll_req = urllib.request.Request(
                f'https://api.replicate.com/v1/predictions/{prediction_id}',
                headers={'Authorization': f'Token {REPLICATE_API_TOKEN}'}
            )
            with urllib.request.urlopen(poll_req) as poll_response:
                result = json.loads(poll_response.read())
            print(f"Poll status: {result.get('status')}")
            if result.get('status') == 'succeeded':
                webp_url = result['output'][0]

                with urllib.request.urlopen(webp_url) as img_response:
                    webp_bytes = img_response.read()

                os.makedirs('generated', exist_ok=True)

                png_filename = f"{uuid.uuid4()}.png"
                png_path = os.path.join('generated', png_filename)

                image = Image.open(BytesIO(webp_bytes)).convert("RGB")
                image.save(png_path, "PNG")

                local_url = f"http://127.0.0.1:5001/generated/{png_filename}"

                print({
                    'image_path': os.path.abspath(png_path),
                    'image_url': local_url
                })
                return {
                    'image_path': os.path.abspath(png_path),
                    'image_url': local_url
                }
            elif result.get('status') == 'failed':
                return {'error': 'Image generation failed'}

        return {'error': 'Timed out'}

    def log_message(self, format, *args):
        print(f"{self.path} - {args[0]}")

if __name__ == '__main__':
    server = ThreadingHTTPServer(('127.0.0.1', 5001), ProxyHandler)
    print('Dream Engine proxy running on http://127.0.0.1:5001')
    server.serve_forever()
