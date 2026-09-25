import asyncio
import json
import sys
import websockets

HOST = "ws://localhost:5000"

async def create_game():
    """Open a create-socket, print the code the server sends first, and return it."""
    ws = await websockets.connect(f"{HOST}/ws/game/create")
    raw = await ws.recv()
    data = json.loads(raw)
    code = data.get("Code")
    print(f"[create] connected, received: {data}")
    return ws, code

async def join_game(code):
    """Open a connect-socket for a given game code."""
    ws = await websockets.connect(f"{HOST}/ws/game/connect?code={code}")
    return ws

async def drain_and_print(ws, label):
    """Wait for the next message and print its Count field, if present."""
    try:
        raw = await asyncio.wait_for(ws.recv(), timeout=2.0)
    except asyncio.TimeoutError:
        print(f"[{label}] no message received (timeout)")
        return
    try:
        data = json.loads(raw)
    except json.JSONDecodeError:
        print(f"[{label}] non-JSON message: {raw!r}")
        return
    count = data.get("count", data.get("Count"))
    print(f"[{label}] count={count}  raw={data}")

async def main():
    # 1. Create a game — this also connects the creator.
    creator_ws, code = await create_game()
    await drain_and_print(creator_ws, "creator after create")

    # 2. Join two more players, one at a time.
    joiner1 = await join_game(code)
    await asyncio.sleep(0.2)  # let the server broadcast
    await drain_and_print(creator_ws, "creator after joiner1")
    await drain_and_print(joiner1,   "joiner1 after join")

    joiner2 = await join_game(code)
    await asyncio.sleep(0.2)
    await drain_and_print(creator_ws, "creator after joiner2")
    await drain_and_print(joiner1,   "joiner1 after joiner2")
    await drain_and_print(joiner2,   "joiner2 after join")

    # 3. Close everyone and check the count drops.
    print("\n[closing joiner2]")
    await joiner2.close()
    await asyncio.sleep(0.3)
    await drain_and_print(creator_ws, "creator after joiner2 left")
    await drain_and_print(joiner1,   "joiner1 after joiner2 left")

    print("\n[closing joiner1]")
    await joiner1.close()
    await asyncio.sleep(0.3)
    await drain_and_print(creator_ws, "creator after joiner1 left")

    await creator_ws.close()
    print("\n[done]")

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        sys.exit(0)