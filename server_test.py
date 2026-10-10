"""Integration test against the actual Docker server (no mocks)."""
import asyncio
import json
import os
import websockets

HOST = os.environ.get("GAME_SERVER_URL", "ws://localhost:5285").rstrip("/")

async def receive(ws, kind):
    raw = await asyncio.wait_for(ws.recv(), timeout=5)
    value = json.loads(raw)
    assert value["Type"] == kind, value
    return value

async def count(ws, expected):
    value = await receive(ws, "playerCount")
    assert value["Count"] == expected, value

async def main():
    async with websockets.connect(HOST + "/ws/game/create") as creator:
        code = (await receive(creator, "gameCode"))["Code"]
        assert len(code) == 6 and code.isalnum(), code
        await count(creator, 1)
        async with websockets.connect(HOST + "/ws/game/connect?code=" + code.lower()) as joiner:
            assert (await receive(joiner, "gameCode"))["Code"] == code
            await count(creator, 2)
            await count(joiner, 2)
            # Split JSON into WebSocket fragments to exercise reassembly.
            command = json.dumps({"Type": "click", "X": 1.25, "Y": -2.5})
            await creator.send([command[:10], command[10:]])
            ack = await receive(creator, "clickAck")
            assert ack["Code"] == code and ack["X"] == 1.25 and ack["Y"] == -2.5, ack
            await creator.send("{broken json")
            await receive(creator, "error")
            await creator.send(json.dumps({"Type": "click", "X": "bad", "Y": 0}))
            await receive(creator, "error")
            await creator.send(json.dumps({"Type": "unknown"}))
            await receive(creator, "error")
            # Errors must not kill the connection.
            await creator.send(json.dumps({"Type": "click", "X": 0, "Y": 0}))
            await receive(creator, "clickAck")
        await count(creator, 1)
    try:
        async with websockets.connect(HOST + "/ws/game/connect?code=INVALID"):
            raise AssertionError("Unknown game unexpectedly accepted")
    except Exception as exc:
        response = getattr(exc, "response", None)
        status = getattr(response, "status_code", getattr(exc, "status_code", None))
        assert status == 404, f"Expected HTTP 404, got {exc!r}"
    print("PASS: create, join, counts, fragmented click, errors, disconnect, missing game")

if __name__ == "__main__":
    asyncio.run(main())
