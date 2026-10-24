import asyncio
import getpass
import os
import socket

from dotenv import load_dotenv
from TikTokLive import TikTokLiveClient
from TikTokLive.events import GiftEvent, LikeEvent, FollowEvent, ShareEvent, JoinEvent

load_dotenv()

USERNAME = os.getenv("TIKTOK_USERNAME", "").strip()
QUEST_IP = os.getenv("QUEST_IP", "").strip()
PORT = int(os.getenv("BRIDGE_PORT", "28991"))
MAX_BRIDGE_QUANTITY = 10000
MAX_ROSES_PER_EVENT = 10

if not USERNAME:
    raise SystemExit("Set TIKTOK_USERNAME in your .env file.")
if not QUEST_IP:
    raise SystemExit("Set QUEST_IP in your .env file.")

# Ask privately: the bridge secret is not stored in .env or echoed in the terminal.
SECRET = getpass.getpass("Enter the ROSE_BRIDGE_SECRET saved in GitHub Actions: ").strip()
if not SECRET:
    raise SystemExit("A bridge secret is required.")

client = TikTokLiveClient(unique_id=USERNAME)


def send_event(event_type: str, quantity: int = 1) -> None:
    """Send one authenticated TikTok event to the Quest mod over the local network."""
    quantity = max(1, min(int(quantity), MAX_BRIDGE_QUANTITY))
    message = f"EVENT|{SECRET}|{event_type}|{quantity}\\n".encode("utf-8")
    with socket.create_connection((QUEST_IP, PORT), timeout=4) as connection:
        connection.settimeout(4)
        connection.sendall(message)
        response = connection.recv(32)
        if not response.startswith(b"OK"):
            raise RuntimeError(f"Quest did not acknowledge the event (response: {response!r}).")


async def relay_event(event_type: str, quantity: int = 1) -> None:
    try:
        await asyncio.to_thread(send_event, event_type, quantity)
        print(f"[{event_type}] Relayed event quantity={quantity} to Quest.")
    except Exception as exc:
        print(f"[BRIDGE ERROR] Could not send {event_type} to {QUEST_IP}:{PORT}: {exc}")


def event_count(event, *names: str, default: int = 1) -> int:
    for name in names:
        value = getattr(event, name, None)
        try:
            if value is not None:
                return max(1, min(int(value), MAX_BRIDGE_QUANTITY))
        except (TypeError, ValueError):
            continue
    return default


@client.on(GiftEvent)
async def on_gift(event: GiftEvent) -> None:
    gift = getattr(event, "gift", None)
    gift_name = getattr(gift, "name", None) or getattr(event, "gift_name", "")
    if str(gift_name).strip().lower() != "rose":
        return

    # Streakable gifts may emit intermediate events before the final count.
    streakable = bool(getattr(gift, "streakable", False)) or getattr(gift, "type", 0) == 1
    if streakable and bool(getattr(event, "streaking", False)):
        return

    quantity = min(event_count(event, "repeat_count", default=1), MAX_ROSES_PER_EVENT)
    await relay_event("ROSE", quantity)


@client.on(LikeEvent)
async def on_like(event: LikeEvent) -> None:
    # LikeEvent.count represents the likes in this event; BoneMenu sets the threshold.
    quantity = event_count(event, "count", "like_count", default=1)
    await relay_event("LIKE", quantity)


@client.on(FollowEvent)
async def on_follow(event: FollowEvent) -> None:
    await relay_event("FOLLOW", 1)


@client.on(ShareEvent)
async def on_share(event: ShareEvent) -> None:
    await relay_event("SHARE", 1)


@client.on(JoinEvent)
async def on_join(event: JoinEvent) -> None:
    await relay_event("JOIN", 1)


if __name__ == "__main__":
    print(f"Connecting to TikTok LIVE @{USERNAME.lstrip('@')}...")
    print(f"Quest bridge target: {QUEST_IP}:{PORT}")
    print("BoneMenu controls choose which event types cause spawns.")
    print("Keep this window open while streaming. Press Ctrl+C to stop.")
    client.run()
