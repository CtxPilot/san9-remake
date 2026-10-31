"""Serve map preview source and geographic datasets on loopback only."""
import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse, parse_qs
from prepare import catalog, prepare


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=28119)
    args = parser.parse_args()
    directory = Path(__file__).resolve().parent
    files = {"/": ("index.html", "text/html"), "/index.html": ("index.html", "text/html"),
             "/style.css": ("style.css", "text/css"), "/app.js": ("app.js", "text/javascript"),
             "/graph.js": ("graph.js", "text/javascript")}
    entries = catalog()
    valid_ids = {entry["id"] for entry in entries}

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            url = urlparse(self.path)
            if url.path == "/favicon.ico":
                self.send_response(204); self.end_headers(); return
            if url.path == "/api/catalog":
                body = json.dumps(entries, ensure_ascii=False).encode("utf-8")
                content_type = "application/json"
            elif url.path == "/api/map":
                scenario_id = parse_qs(url.query).get("id", ["19"])[0]
                if scenario_id not in valid_ids:
                    self.send_error(404); return
                body = prepare(scenario_id).read_bytes()
                content_type = "application/json"
            elif url.path in files:
                name, content_type = files[url.path]
                body = (directory / name).read_bytes()
            else:
                self.send_error(404); return
            self.send_response(200)
            self.send_header("Content-Type", content_type + "; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers(); self.wfile.write(body)

    server = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    print(f"Map network preview: http://127.0.0.1:{args.port}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
