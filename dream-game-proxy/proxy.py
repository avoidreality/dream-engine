from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
import json
import urllib.request
import time
import os
import uuid
from PIL import Image
from io import BytesIO
from secrets import ANTHROPIC_API_KEY, REPLICATE_API_TOKEN
import textwrap
from reportlab.lib.pagesizes import letter
from reportlab.lib.utils import ImageReader
from reportlab.pdfgen import canvas
from reportlab.platypus import Paragraph
from reportlab.lib.styles import getSampleStyleSheet
from reportlab.lib.styles import ParagraphStyle
from xml.sax.saxutils import escape

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
EXPORTS_DIR = os.path.join(BASE_DIR, "exports")
COVER_IMAGE_PATH = os.path.join(
    BASE_DIR,
    "assets",
    "dream_engine_cover.png"
)

ANTHROPIC_API_KEY = ANTHROPIC_API_KEY
REPLICATE_API_TOKEN = REPLICATE_API_TOKEN

class ProxyHandler(BaseHTTPRequestHandler):

    def draw_paragraph(self, pdf, text, x, y, width, height, style):
        paragraph = Paragraph(text, style)
        w, h = paragraph.wrap(width, height)
        paragraph.drawOn(pdf, x, y - h)
        return y - h

    def export_pdf(self, data):
        os.makedirs(EXPORTS_DIR, exist_ok=True)

        dream = data.get('dream', 'Untitled dream')
        obstacles = data.get('obstacles', '')
        style = data.get('style', 'Unspecified')
        created_at = data.get('created_at', '')
        chapters = data.get('chapters', [])

        pdf_filename = f"dream-book-{uuid.uuid4()}.pdf"
        pdf_path = os.path.join(EXPORTS_DIR, pdf_filename)

        pdf = canvas.Canvas(pdf_path, pagesize=letter)
        page_width, page_height = letter

        metadata_style = ParagraphStyle(
            name="Metadata",
            fontName="Helvetica",
            fontSize=12,
            leading=17,
            wordWrap="CJK"
        )

        # --- COVER PAGE ---
        if os.path.isfile(COVER_IMAGE_PATH):
            self.draw_fitted_image(
                pdf,
                COVER_IMAGE_PATH,
                45,
                140,
                page_width - 90,
                page_height - 230
            )

        pdf.setFont("Helvetica-Bold", 30)
        pdf.drawCentredString(
            page_width / 2,
            100,
            "DREAM ENGINE"
        )

        pdf.setFont("Helvetica", 14)
        pdf.drawCentredString(
            page_width / 2,
            75,
            "An existential dream record"
        )

        pdf.showPage()

        # --- METADATA PAGE ---
        pdf.setFont("Helvetica-Bold", 22)
        pdf.drawString(60, page_height - 80, "Dream Metadata")

        usable_width = page_width - 120
        y = page_height - 125
        
        y = self.draw_paragraph(
            pdf,
            f"<b>Dream:</b> {escape(dream)}",
            60,
            y,
            usable_width,
            250,
            metadata_style
        )

        y -= 20

        y = self.draw_paragraph(
            pdf,
            f"<b>Obstacles:</b> {escape(obstacles)}",
            60,
            y,
            usable_width,
            250,
            metadata_style
        )

        y -= 18
        pdf.drawString(60, y, f"Style: {style}")

        y -= 24
        pdf.drawString(60, y, f"Created: {created_at}")

        pdf.showPage()

        # --- CHAPTER PAGES ---
        for index, chapter in enumerate(chapters, start=1):
            chapter_type = chapter.get('chapterType', 'Chapter')
            chapter_text = chapter.get('chapterText', '')
            image_ref = chapter.get('imageUrl', '') or chapter.get("imagePath", '')
            chosen_action = chapter.get('chosenAction', '')

            # Image page
            pdf.setFont("Helvetica-Bold", 20)
            pdf.drawString(
                45,
                page_height - 55,
                f"{index}. {chapter_type}"
            )

            if image_ref:
                self.draw_fitted_image(
                    pdf,
                    image_ref,
                    45,
                    75,
                    page_width - 90,
                    page_height - 160
                )
            else:
                pdf.setFont("Helvetica-Oblique", 14)
                pdf.drawCentredString(
                    page_width / 2,
                    page_height / 2,
                    "No image was saved for this chapter."
                )

            pdf.showPage()

            # Text page
            pdf.setFont("Helvetica-Bold", 20)
            pdf.drawString(
                60,
                page_height - 75,
                f"{index}. {chapter_type}"
            )

            y = page_height - 125

            if chosen_action:
                pdf.setFont("Helvetica-Oblique", 12)
                pdf.drawString(
                    60,
                    y,
                    f"Choice: {chosen_action}"
                )
                y -= 34

            pdf.setFont("Helvetica", 15)
            self.draw_wrapped_text(
                pdf,
                chapter_text,
                60,
                y,
                width_chars=68,
                line_height=24
            )

            pdf.showPage()

        pdf.save()

        pdf_url = (
            f"http://127.0.0.1:5001/exports/{pdf_filename}"
        )

        print("Dream book exported:", pdf_path)

        return {
            'pdf_path': os.path.abspath(pdf_path),
            'pdf_url': pdf_url
        }

    def draw_fitted_image(
            self,
            pdf,
            image_ref,
            x,
            y,
            max_width,
            max_height
    ):
        if image_ref.startswith("http"):
            image_data = urllib.request.urlopen(image_ref).read()
            image = ImageReader(BytesIO(image_data))
        else:
            image = ImageReader(image_ref)

        image_width, image_height = image.getSize()

        scale = min(
            max_width / image_width,
            max_height / image_height
        )

        draw_width = image_width * scale
        draw_height = image_height * scale

        draw_x = x + (max_width - draw_width) / 2
        draw_y = y + (max_height - draw_height) / 2

        pdf.drawImage(
            image,
            draw_x,
            draw_y,
            width=draw_width,
            height=draw_height,
            preserveAspectRatio=True,
            mask='auto'
        )

    def draw_wrapped_text(
            self,
            pdf,
            text,
            x,
            y,
            width_chars=72,
            line_height=20
    ):
        lines = textwrap.wrap(
            text,
            width=width_chars
        )

        for line in lines:
            pdf.drawString(x, y, line)
            y -= line_height

        return y

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
        elif self.path == '/export-pdf':
            result = self.export_pdf(data)
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

        elif self.path.startswith('/exports/'):
            filename = self.path.replace('/exports/', '')
            filepath = os.path.join(EXPORTS_DIR, filename)

            if not os.path.isfile(filepath):
                self.send_response(404)
                self.end_headers()
                return

            with open(filepath, 'rb') as f:
                pdf_bytes = f.read()

            self.send_response(200)
            self.send_cors_headers()
            self.send_header('Content-Type', 'application/pdf')
            self.send_header(
                'Content-Disposition',
                f'attachment; filename="{filename}"'
            )
            self.send_header('Content-Length', len(pdf_bytes))
            self.end_headers()
            self.wfile.write(pdf_bytes)

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
