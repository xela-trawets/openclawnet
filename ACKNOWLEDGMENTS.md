# Acknowledgments

OpenClawNet's built-in tools and infrastructure are powered by excellent open-source .NET libraries. This page lists the libraries we depend on per tool, with links to the original repositories and authors so we can give credit where credit is due.

If a library you maintain is missing from this list, please open an issue.

## Built-in tools

| Tool | Library / Package | Author / Maintainer | License |
| --- | --- | --- | --- |
| `markdown_convert` | [`ElBruno.MarkItDotNet`](https://github.com/elbruno/ElBruno.MarkItDotNet) | [@elbruno](https://github.com/elbruno) | MIT |
| `text_to_image` | [`ElBruno.Text2Image.Cpu`](https://github.com/elbruno/ElBruno.Text2Image) (Stable Diffusion 1.5 via ONNX Runtime) | [@elbruno](https://github.com/elbruno) | MIT |
| `text_to_speech` | [`ElBruno.QwenTTS`](https://github.com/elbruno/ElBruno.QwenTTS) (Qwen3-TTS 0.6B WAV synthesis) | [@elbruno](https://github.com/elbruno) | MIT |
| `embeddings` | [`ElBruno.LocalEmbeddings`](https://github.com/elbruno/ElBruno.LocalEmbeddings) (ONNX text embeddings + Microsoft.Extensions.AI) | [@elbruno](https://github.com/elbruno) | MIT |
| `youtube_transcript` | [`YoutubeExplode`](https://github.com/Tyrrrz/YoutubeExplode) | [@Tyrrrz](https://github.com/Tyrrrz) (Alexey Golub) | LGPL-3.0 |
| `calculator` | [`NCalcSync`](https://github.com/ncalc/ncalc) | NCalc maintainers | MIT |
| `github` | [`Octokit.NET`](https://github.com/octokit/octokit.net) | The Octokit team & contributors | MIT |
| `image_edit` | [`SixLabors.ImageSharp`](https://github.com/SixLabors/ImageSharp) | [Six Labors](https://sixlabors.com/) | Apache-2.0 (Six Labors Split License) |
| `html_query` | [`AngleSharp`](https://github.com/AngleSharp/AngleSharp) | The AngleSharp team | MIT |
| `browser` | [`PuppeteerSharp`](https://github.com/hardkoded/puppeteer-sharp) | [@hardkoded](https://github.com/hardkoded) | MIT |
| `web_fetch` | `System.Net.Http` (built into .NET) | Microsoft | MIT |
| `file_system`, `shell`, `scheduler` | .NET BCL only | Microsoft | MIT |

## Infrastructure

| Concern | Library | Notes |
| --- | --- | --- |
| Secrets at rest | [`Microsoft.AspNetCore.DataProtection`](https://github.com/dotnet/aspnetcore) | Encrypts the `Secrets` table values; keys persisted under `Storage:RootPath/dataprotection-keys`. |
| Distributed app orchestration | [.NET Aspire](https://github.com/dotnet/aspire) | AppHost / ServiceDefaults wiring. |
| Storage | [`Microsoft.EntityFrameworkCore.Sqlite`](https://github.com/dotnet/efcore) | Backing store for profiles, conversations, MCP servers, and secrets. |
| MCP runtime | [`ModelContextProtocol`](https://github.com/modelcontextprotocol/csharp-sdk) | MCP client/server runtime for the Tools/MCP catalog. |

## License notes

OpenClawNet itself is MIT licensed. The libraries above retain their own licenses; please refer to each project's repository for the full license text and any attribution requirements.
