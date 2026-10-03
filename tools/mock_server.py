# -*- coding: utf-8 -*-
"""本地 mock SSE 服务器：模拟 OpenAI 兼容的 /v1/chat/completions 流式接口。

用法: python tools/mock_server.py   （监听 127.0.0.1:8787）
用于在没有真实 API Key 时验证 QuickAsk 的流式问答与图片发送链路。
"""
import json
import time
from http.server import BaseHTTPRequestHandler, HTTPServer


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_POST(self):
        length = int(self.headers.get("Content-Length", 0))
        raw = self.rfile.read(length) if length else b""
        try:
            body = json.loads(raw or b"{}")
        except ValueError:
            body = {}
        msgs = body.get("messages", [])
        has_image = any(isinstance(m.get("content"), list) for m in msgs)
        img_bytes = sum(
            len(json.dumps(m["content"]).encode("utf-8"))
            for m in msgs if isinstance(m.get("content"), list)
        )
        note = f"已收到你的截图（消息体约 {img_bytes // 1024} KB）。" if has_image else ""
        print(f"[mock] model={body.get('model')} msgs={len(msgs)} image={has_image} body={len(raw)//1024}KB")

        text = (
            f"这是模拟回答。{note}\n\n"
            "支持 **加粗**、`行内代码` 和列表：\n"
            "- 第一条要点\n"
            "- 第二条要点\n\n"
            "```python\nprint('代码块也支持')\n```\n"
            "第二段文字，用来验证流式输出与自动换行。"
        )

        self.send_response(200)
        self.send_header("Content-Type", "text/event-stream; charset=utf-8")
        self.send_header("Cache-Control", "no-cache")
        self.send_header("Connection", "close")
        self.end_headers()
        for i in range(0, len(text), 6):
            chunk = {"choices": [{"delta": {"content": text[i:i + 6]}}]}
            self.wfile.write(f"data: {json.dumps(chunk, ensure_ascii=False)}\n\n".encode("utf-8"))
            self.wfile.flush()
            time.sleep(0.02)
        self.wfile.write(b"data: [DONE]\n\n")

    def log_message(self, *args):  # 静默默认日志
        pass


if __name__ == "__main__":
    print("mock server on http://127.0.0.1:8787/v1")
    HTTPServer(("127.0.0.1", 8787), Handler).serve_forever()
