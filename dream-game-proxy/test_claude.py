import requests

payload = {
    "prompt": "A person dreams of becoming a musician but faces obstacles of no money and family disapproval. Write a short dramatic chapter event of 2-3 sentences that they encounter on their journey. Make it emotional and specific."
}

response = requests.post('http://localhost:5001/claude', json=payload)
# data = response.json()

print("Status code:", response.status_code)
print("Raw response:", response.text)

# Print the response
# print(data['content'][0]['text'])

