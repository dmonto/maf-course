"""Exercise probe failures against a local synthetic HTTP service."""
import http.server
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import time
import unittest


class ProbeTests(unittest.TestCase):
    def run_probe(self, scenario):
        seen = []

        class Handler(http.server.BaseHTTPRequestHandler):
            def log_message(self, *_):
                pass

            def reply(self, status, body):
                data = json.dumps(body).encode()
                self.send_response(status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                try:
                    self.wfile.write(data)
                except (BrokenPipeError, ConnectionResetError):
                    pass

            def do_GET(self):
                if scenario == "liveness_timeout" and self.path == "/health/live":
                    time.sleep(1.5)
                self.reply(200, {"status": "ok", "release": "14.1.0", "mode": "azure",
                                 "promptVersion": "status-v1", "instance": "synthetic"})

            def do_POST(self):
                request = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
                seen.append(request["service"])
                if scenario == "functional_timeout":
                    time.sleep(1.5)
                if scenario == "server_deadline":
                    self.reply(504, {"requestId": "a" * 32, "error": "request_cancelled", "phase": "identity",
                                     "reason": "request_deadline", "timeoutSeconds": 90,
                                     "elapsedMs": 90000, "message": "unit14-sensitive-marker"})
                    return
                service = request["service"]
                status = "degraded" if service == "vpn" else "healthy"
                summary = "Synthetic evidence: VPN connectivity is degraded." if service == "vpn" else "Synthetic evidence: email is healthy."
                self.reply(200, {"requestId": "b" * 32, "service": service, "status": status,
                                 "summary": summary, "release": "14.1.0", "mode": "azure",
                                 "promptVersion": "status-v1", "instance": "synthetic"})

        server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        server.daemon_threads = True
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            with tempfile.TemporaryDirectory(prefix="unit14 probe ") as directory:
                output = Path(directory) / "report.json"
                result = subprocess.run([sys.executable, str(Path(__file__).parents[1] / "Python/smoke.py"),
                                         "--base", f"http://127.0.0.1:{server.server_port}",
                                         "--release", "14.1.0", "--mode", "azure", "--timeout", "1",
                                         "--requests", "4", "--output", str(output)],
                                        capture_output=True, text=True, timeout=12)
                raw = output.read_text()
                self.assertNotIn("unit14-sensitive-marker", raw)
                return result.returncode, json.loads(raw), seen
        finally:
            server.shutdown()
            server.server_close()

    def test_success(self):
        code, report, seen = self.run_probe("success")
        self.assertEqual(code, 0)
        self.assertTrue(report["passed"])
        self.assertEqual(len(seen), 4)

    def test_server_failure_keeps_phase_and_stops(self):
        code, report, seen = self.run_probe("server_deadline")
        self.assertEqual(code, 1)
        self.assertEqual(report["failureStage"], "functional")
        self.assertEqual(report["results"][0]["phase"], "identity")
        self.assertEqual(report["results"][0]["httpStatus"], 504)
        self.assertEqual(len(seen), 1)

    def test_transport_timeout_keeps_result(self):
        code, report, seen = self.run_probe("functional_timeout")
        self.assertEqual(code, 1)
        self.assertEqual(report["failureStage"], "functional")
        self.assertEqual(report["results"][0]["failureType"], "TimeoutError")
        self.assertEqual(len(seen), 1)

    def test_liveness_timeout_identifies_health_path(self):
        code, report, seen = self.run_probe("liveness_timeout")
        self.assertEqual(code, 1)
        self.assertEqual(report["failureStage"], "liveness")
        self.assertEqual(report["failurePath"], "/health/live")
        self.assertEqual(seen, [])


if __name__ == "__main__":
    unittest.main()
