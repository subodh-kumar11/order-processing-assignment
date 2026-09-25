# Job-description mapping and solution scope

The supplied JD lists Python, Java, Node.js, Go/Golang, R, C++, C#, Ruby, Generative AI and AI Tools as mandatory skills. React and Angular are desirable. Responsibilities include working across languages, using AI tools, maintainable and efficient code, collaboration and code review.

The assignment permits any backend language (examples: Java or .NET). It requests five order operations and a five-minute background job. It encourages AI assistance and asks for an explanation of its use, issues and corrections. The next round includes a coding walkthrough and design patterns.

The JD does not require incorporating every listed language into this assignment.

| JD skill | Relevance | Project evidence |
| --- | --- | --- |
| C# | Main backend choice | ASP.NET Core endpoints, typed records, decimal arithmetic, state transitions |
| Node.js | Supporting runtime | JavaScript integration tests using the built-in Node test runner and HTTP client |
| AI Tools | Explicit assignment expectation | ChatGPT/Codex assistance documented in AI_USAGE.md |
| Generative AI | Role skill; optional runtime feature here | AI-assisted development demonstrated; no LLM application feature claimed |
| Clean, maintainable code | Directly relevant | Focused components, explicit invariants, bounded inputs and documented tradeoffs |
| Code review / best practices | Directly relevant | Git history, CI configuration and behavioral tests |
| Python, Java, Go, C++, Ruby | Alternative backend choices | Not needed alongside C# for this scope |
| R | No specific role in this task | Analytics would be a separate requirement |
| React / Angular | Optional frontend skills | Not used because the assignment asks for a backend |

Python helped inspect the PDFs during preparation; it is not part of the application's runtime.

## Additional engineering skills demonstrated

REST, HTTP status codes, validation, concurrency control, persistence, hosted background services, cancellation, dependency injection, integration testing, Git, CI and container packaging. These are solution choices, not claims that the JD explicitly names ASP.NET Core, Docker, GitHub Actions or a particular database.

## Stack decision

C# is named in the JD and .NET is permitted by the assignment. Node.js provides an independent HTTP test client in a second listed runtime without adding another production service. Running the API needs the .NET 10 SDK; tests additionally need Node.js 24.

The supplied PDFs confirm the existing implementation's scope. The persistent JSON store is suitable for a small single-process submission, but it rewrites the dataset on each mutation. A transactional database is the next improvement for larger workloads or multiple instances. Do not describe this version as distributed or production-hardened.

## Requirements traceability

| Requirement | Implementation | Verification |
| --- | --- | --- |
| Create multi-item order | POST /orders | Exact totals, validation, concurrent creates |
| Fetch by ID | GET /orders/{id} | Existing and missing IDs |
| Update status | PATCH /orders/{id}/status | Sequence, rejected skips/reversals, terminal states |
| Process pending every 5 minutes | ProcessingWorker; default 300 seconds | Actual worker tested at one-second interval |
| List and filter | GET /orders | Filtering, pagination, invalid queries |
| Cancel pending only | POST /orders/{id}/cancel | Cancellation, retries, conflicts and races |
| Explain AI use | AI_USAGE.md and VERIFICATION.md | Design risks separated from observed build issue |
| Repository submission | Git history and GITHUB.md | Remote status reported separately |
