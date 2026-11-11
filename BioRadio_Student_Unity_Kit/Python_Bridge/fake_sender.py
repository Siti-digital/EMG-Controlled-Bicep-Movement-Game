import json
import socket
import time
import math

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
start = time.time()
print("Starting fake_sender.py sending to 127.0.0.1:9050... Press Ctrl+C to stop.")

try:
    while True:
        t = time.time() - start
        # smooth 0 -> 0.3 -> 0, every 4 s
        activation = 0.3 * 0.5 * (1.0 - math.cos(2.0 * math.pi * t / 4.0))
        msg = {
            "channel": 1,
            "sender_time": round(time.time(), 3),
            "activation": round(activation, 4),
            "filter": "FAKE"
        }
        sock.sendto(json.dumps(msg).encode(), ("127.0.0.1", 9050))
        time.sleep(0.02) # about 50 messages per second
except KeyboardInterrupt:
    print("Stopped fake_sender.")
