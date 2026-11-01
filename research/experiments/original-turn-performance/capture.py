"""Observe the exact registered original; control via an output-local JSON file."""
from __future__ import annotations

import argparse
import ctypes
from ctypes import wintypes
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import time
from datetime import datetime, timezone

import frida

ROOT = Path(__file__).resolve().parents[3]
EXPECTED_SHA256 = "d20794aeff67301ec2bf8c3becb1e9944c68c6c0588fbfd4bf04e8597f0e5028"
EXPECTED_SIZE = 2_636_800


def process_path(pid: int) -> Path:
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.OpenProcess(0x1000, False, pid)
    if not handle:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        buffer = ctypes.create_unicode_buffer(32768)
        length = wintypes.DWORD(len(buffer))
        if not kernel.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(length)):
            raise ctypes.WinError(ctypes.get_last_error())
        return Path(buffer.value)
    finally:
        kernel.CloseHandle(handle)


def write_json(path: Path, value: object) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int)
    parser.add_argument("--wait-name", default="San9PK.exe")
    parser.add_argument("--output", type=Path, default=ROOT / "local/generated/research/original-turn-performance/session")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--mode", choices=["boundary", "detail", "opening"], default="boundary")
    args = parser.parse_args()
    device = frida.get_local_device()
    child = None
    session = None
    args.output.mkdir(parents=True, exist_ok=False)
    events = args.output / "events.jsonl"
    control = args.output / "control.json"
    status = args.output / "status.json"
    last_control = None
    detached = False
    stop_requested = False
    try:
        if args.self_test:
            child = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(120)"], creationflags=subprocess.CREATE_NO_WINDOW)
            pid = child.pid
        elif args.pid:
            pid = args.pid
        else:
            print(f"WAITING_FOR_PROCESS {args.wait_name}", flush=True)
            while True:
                matches = [p for p in device.enumerate_processes() if p.name.lower() == args.wait_name.lower()]
                if len(matches) > 1:
                    raise RuntimeError("Multiple matching game processes; use --pid")
                if matches:
                    pid = matches[0].pid
                    break
                time.sleep(.5)
        executable = process_path(pid)
        identity = {"path": str(executable), "size": executable.stat().st_size,
                    "sha256": hashlib.sha256(executable.read_bytes()).hexdigest()}
        if not args.self_test and (identity["size"] != EXPECTED_SIZE or identity["sha256"] != EXPECTED_SHA256):
            raise RuntimeError(f"Not SRC-SAN9PK-TC-101: {identity}")
        agent_source = Path(__file__).with_name("agent.js").read_bytes()
        manifest = {"utc": datetime.now(timezone.utc).isoformat(), "pid": pid, "executable": identity,
                    "python": sys.version, "frida": frida.__version__, "platform": platform.platform(),
                    "cpu": platform.processor(), "logical_cpus": os.cpu_count(),
                    "agent_sha256": hashlib.sha256(agent_source).hexdigest()}
        write_json(args.output / "manifest.json", manifest)
        (args.output / "agent.js").write_bytes(agent_source)
        session = device.attach(pid)

        def on_detached(reason, crash=None):
            nonlocal detached
            detached = True
            print(f"DETACHED {reason}", flush=True)

        session.on("detached", on_detached)
        script = session.create_script(agent_source.decode("utf-8"))

        def on_message(message, data):
            record = message.get("payload", message)
            with events.open("a", encoding="utf-8") as stream:
                stream.write(json.dumps(record, ensure_ascii=False) + "\n")
            if message["type"] == "error":
                print("AGENT_ERROR " + json.dumps(record, ensure_ascii=False), flush=True)
            elif record.get("kind") in {"ready", "turn_begin", "turn_end", "armed", "ai_phase_end"}:
                display = {key: value for key, value in record.items() if key not in {"automatic_actors", "opening_events", "probes", "days"}}
                print(json.dumps(display, ensure_ascii=False), flush=True)
            if record.get("kind") == "turn_end":
                write_json(args.output / f"turn-{record['run']:03d}.json", record)

        script.on("message", on_message)
        script.load()
        ready = script.exports_sync.initialize(args.self_test, args.mode)
        write_json(args.output / "ready.json", ready)
        if args.self_test:
            print("NATIVE_PROFILER_SELF_TEST_OK " + json.dumps(ready), flush=True)
            return 0
        print(f"COLLECTOR_READY_UNARMED control={control}", flush=True)
        while not detached:
            if control.exists():
                content = control.read_bytes()
                if content != last_control:
                    command = json.loads(content.decode("utf-8-sig"))
                    action = command["action"]
                    if action == "arm":
                        script.exports_sync.arm(command.get("label", ""))
                    elif action == "disarm":
                        script.exports_sync.disarm()
                    elif action == "abort":
                        script.exports_sync.abort()
                    elif action == "stop":
                        stop_requested = True
                    elif action != "status":
                        raise ValueError(f"Unknown action: {action}")
                    last_control = content
            state = script.exports_sync.status()
            write_json(status, state)
            if stop_requested and state["active"] is None:
                break
            time.sleep(.5)
        return 0
    finally:
        try:
            if session is not None and not detached:
                session.detach()
        finally:
            if child is not None:
                child.terminate()
                child.wait(timeout=10)


if __name__ == "__main__":
    raise SystemExit(main())
