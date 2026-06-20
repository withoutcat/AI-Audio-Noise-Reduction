const https = require("https");

const apiKey = process.env.DASHSCOPE_API_KEY;
if (!apiKey) {
  console.error("DASHSCOPE_API_KEY is not set in this process.");
  process.exit(1);
}

const body = JSON.stringify({
  model: "qwen-plus",
  messages: [
    { role: "user", content: "Reply with exactly: qwen-ok" },
  ],
  max_tokens: 16,
  temperature: 0,
});

const req = https.request(
  {
    hostname: "dashscope.aliyuncs.com",
    path: "/compatible-mode/v1/chat/completions",
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
      "Content-Length": Buffer.byteLength(body),
    },
  },
  (res) => {
    let data = "";
    res.setEncoding("utf8");
    res.on("data", (chunk) => {
      data += chunk;
    });
    res.on("end", () => {
      if (res.statusCode < 200 || res.statusCode >= 300) {
        console.error(`DashScope returned HTTP ${res.statusCode}`);
        console.error(data.slice(0, 1000));
        process.exit(1);
      }

      const parsed = JSON.parse(data);
      const content = parsed.choices?.[0]?.message?.content ?? "";
      console.log(`HTTP ${res.statusCode}`);
      console.log(`Model reply: ${content}`);
    });
  }
);

req.on("error", (error) => {
  console.error(error.message);
  process.exit(1);
});

req.write(body);
req.end();
