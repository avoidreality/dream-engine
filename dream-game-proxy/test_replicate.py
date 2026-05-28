import requests

print("starting 'test replicate'...")

payload = {
    "prompt": "a musician standing alone on a dark stage with a single spotlight, dramatic, cinematic, digital art"
}

response = requests.post('http://127.0.0.1:5001/image', json=payload)
print("Status code:", response.status_code)
print("Raw response:", response.text)
