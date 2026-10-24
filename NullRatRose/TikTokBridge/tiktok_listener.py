import asyncio
import getpass
import os
import socket
import sys

from dotenv import load_dotenv
from TikTokLive import TikTokLiveClient
from TikTokLive.events import GiftEvent

load_dotenv()

USERNAME = os.getenv("TIKTOK_USERNAME", "").strip()
QUEST_IP = os.getenv("QUEST_IP", "").strip()
PORT = int(os.getenv("BRIDGE_PORT", "28991"))
MAX_ROSES_PER_EVENT = 10

if not USERNAME:
    raise SystemExit("Set TIKTOK_USERNAME in your .env file.")
if not QUEST_IP:
    raise SystemExit("Set QUEST_IP in your .env file.")

# Read the shared secret without echoing it or saving it in the .env file.
SECRET = getpass.getpass("Enter the ROSE_BRIDGE_SECRET saved in GitHub Actions: ").strip()
if not SECRET:
    raise SystemExit("A bridge secret is required.")

client = TikTokLiveClient(unique_id=USERNAME)


def send_rose_command(quantity: int) -> None:
    message = f"ROSE|{SECRET}|{quantity}\n".encode("utf-8")
    with socket.create_connection((QUEST_IP, PORT), timeout=4) as connection:
        connection.settimeout(4)
        connection.sendall(message)
        response = connection.recv(32)
        if not response.startswith(b"OK"):
            raise RuntimeError(f"Quest did not acknowledge the event (response: {response!r}).")


@client.on(GiftEvent)
async def on_gift(event: GiftEvent) -> None:
    gift = getattr(event, "gift", None)
    gift_name = getattr(gift, "name", None) or getattr(event, "gift_name", "")
    if str(gift_name).strip().lower() != "rose":
        return

    # Do not react to intermediate frames of a streakable gift. Handle its final event once.
    streakable = bool(getattr(gift, "streakable", False)) or getattr(gift, "type", 0) == 1
    if streakable and bool(getattr(event, "streaking", False)):
        return

    try:
        quantity = int(getattr(event, "repeat_count", 1) or 1)
    except (TypeError, ValueError):
        quantity = 1
    quantity = max(1, min(quantity, MAX_ROSES_PER_EVENT))

    try:
        await asyncio.to_thread(send_rose_command, quantity)
        print(f"[ROSE] Relayed {quantity} Rose gift(s) from TikTok LIVE. Expected {quantity * 5} Null Rats.")
    except Exception as exc:
        print(f"[BRIDGE ERROR] Could not reach Quest at {QUEST_IP}:{PORT}: {exc}")


if __name__ == "__main__":
    print(f"Connecting to TikTok LIVE @{USERNAME.lstrip('@')}...")
    print(f"Quest bridge target: {QUEST_IP}:{PORT}")
    print("Keep this window open while streaming. Press Ctrl+C to stop.")
    client.run()
