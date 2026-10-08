# Denials Command Center

Answers the practice manager's three Monday questions for Gulfview Physician Partners: money stuck in denials and how much is recoverable, why claims are denied and what was preventable, and what each specialist should work on today.

## The answers today (30 September 2026)

1. **Money stuck and recoverable.** 155 open denials hold **$31,145**.
   - **Can still be recovered: $11,650** (55 claims). About **$7,090** of it is expected back, based on each payer's history.
   - **Depends on other coverage: $2,975** (14 claims), about $1,811 expected back if the other insurer pays.
   - **Already lost: $16,520.** That is $7,615 past the appeal deadline (38 claims) plus $8,905 the payer's policy does not pay (48 claims).
   - **Urgent:** 5 recoverable claims ($950) must be appealed or resubmitted within 14 days, and 15 claims ($2,890) within 30 days.
2. **Why denied and what was preventable.** **131 of 155 denials ($25,880, 83%) could have been caught before billing.**
   - By cause: coding errors $10,915 (diagnosis $5,295, modifier $3,365, frequency $2,255), provider credentialing $4,460, missing authorization $4,210, medical necessity $3,725, eligibility $3,660, timely filing $1,725, duplicates $910.
   - Only $1,540 (7 claims) is a payer error worth appealing as such.
3. **What each person works on today.** Each specialist's worklist is ordered by what can still be won, then by expected money and how close the deadline is (see [Worklist](#worklist)).

**Reconciliation:** the payer files total $118,220.36 once a resent duplicate file ($47,461.62) is excluded. That splits into $117,959.96 paid to our claims and $260.40 paid to claim numbers from another practice. The difference is **$0.00**, and 68 data exceptions are listed with a reason and a next step.

## Status against the brief

| Part | Status |
|---|---|
| **A. Problem memo** | Done: [docs/problem-memo.md](docs/problem-memo.md) |
| **B. Ingestion and reconciliation** | Done. One record per claim with its full payment and denial history. Re-runs are idempotent (byte-identical). Unmatched or inconsistent data goes to Exceptions with a reason, and the Reconciliation page balances to $0.00. |
| **C. AI denial analysis** | Done:<br>• Every open denial has a root cause, owning team, preventable flag and next action.<br>• Appealable claims get a draft letter that cites the exact payer policy section, and the AI's output is validated against those sections.<br>• Confidence is shown, and low-confidence results and AI/rules disagreements go to a review queue.<br>• It works without AI (rule-based template letters).<br>• File text is treated as untrusted: instruction-like worklog notes are flagged and never followed, and the AI receives only de-identified facts.<br>• **Evaluation** ([docs/ai-evaluation.md](docs/ai-evaluation.md)): against the 40 expert labels, the rules engine scores 40/40. Gemini, without seeing the rules' answer, scores 37/40 on root cause and 31/40 on all three fields. Its misses are medical-necessity ownership and preventability conventions; the report explains why, and how disagreements reach a person. |
| **D. Worklist application** | Done:<br>• Manager and Denials Specialist roles<br>• A prioritised queue with server-side search, sort and paging<br>• A claim page with the full timeline from billed to paid or denied, plus every action taken<br>• Every change audit-logged with who, what, when, before and after |
| **E. Manager analytics and prevention** | **Partly done.** Money at risk by recovery bucket is on the worklist. Not built yet: trends, breakdowns by payer, reason, provider, coder and facility, the prevention view, and the JSON rule export. |
| One-command start, no committed keys | Done: `docker compose up --build`; every setting is listed below |
| Tests where mistakes cost money | 313 automated tests, covering:<br>• X12 parsing<br>• reconciliation goldens<br>• idempotent re-ingestion<br>• recoverability and deadlines<br>• rules against all 40 labels<br>• AI validation, caching and cost limits<br>• access control, audit, rate limits and configuration |
| Problem memo, AI evaluation report, AI usage log | Done: [problem memo](docs/problem-memo.md), [AI evaluation](docs/ai-evaluation.md), [AI usage log](docs/ai-usage-log.md) |
| Screen recording, total hours | Provided with the submission email |

## Run

Requires Docker (Docker Desktop on Windows or Mac). From the repository root:

```bash
docker compose up --build
```

That is the only command. The first build takes a few minutes. When it finishes:
- **Web app:** http://localhost:3000. Sign in as `manager` with password `change-me-locally`; see [Sign in](#sign-in) for the specialists.
- **API:** http://localhost:8080 (`/api/health` needs no sign-in).

On every start the API applies database migrations and loads `data/`. The first start also creates the users and work items. Loading is idempotent: unchanged files are a no-op, and a forced rebuild (`POST /api/ingestion/run?force=true`) writes identical rows.

A `.env` file is optional. Copy `.env.example` to `.env` when you want to:
- set your own database password (`POSTGRES_PASSWORD`) or seeded-user password (`Auth__SeedPassword`, read only on the first start)
- turn on AI drafting with `Gemini__ApiKey`
- turn on email password reset with the four `Graph__*` values

After changing `.env`, run `docker compose up -d` again. Start from an empty database with `docker compose down -v`, then `docker compose up --build`.

The API and database ports are bound to 127.0.0.1 only. The API trusts the `X-Forwarded-For` header from the web container's nginx to rate-limit sign-in per client, and that binding keeps the header from being forged from outside the machine.

Every `/api` call except `/api/health` and `/api/auth/login` needs a signed-in session, and the endpoints listed below are manager-only unless noted.

**New remittance file:** copy it into `data/remits/`, then run `docker compose restart api`. Alternatively, sign in as the manager and trigger loading with a cookie jar:
```bash
curl -c jar.txt -H "Content-Type: application/json" -d '{"username":"manager","password":"change-me-locally"}' http://localhost:8080/api/auth/login
curl -b jar.txt -X POST http://localhost:8080/api/ingestion/run
```

### Without Docker
Start a local Postgres 17 with a `denials` database and user, then from the repository root:
```bash
export ConnectionStrings__Denials="Host=localhost;Database=denials;Username=denials;Password=<your password>"
export Ingestion__DataDirectory="$PWD/data"
export Auth__SeedPassword="change-me-locally"
dotnet run --project src/DenialsCommandCenter.Api --urls http://localhost:8080
```

Web app, with a dev server that proxies `/api` to `http://localhost:8080`:
```bash
cd web && npm install && npm run dev
```

## Sign in
The users are `manager` (Manager) and `karan`, `priya`, `anjali`, `rahul` (Denials Specialists). They all use the password from `Auth__SeedPassword`, which defaults to `change-me-locally`. Users are created on the first start, when the users table is empty.

## Environment variables
| Variable | Purpose | Default |
|---|---|---|
| `POSTGRES_PASSWORD` | Database password (compose builds the connection string from it) | `local_dev_only` (local only) |
| `ConnectionStrings__Denials` | Postgres connection string; required when running without compose | set by compose |
| `Auth__SeedPassword` | Password for the seeded users; read only when they are created on first start | `change-me-locally` |
| `Gemini__ApiKey` | Optional. Enables AI-drafted letters; without it the rule-based template letter is used | unset |
| `Graph__TenantId`, `Graph__ClientId`, `Graph__ClientSecret`, `Graph__SenderAddress` | Optional. Password reset by email through Microsoft Graph; offered only when all four are set | unset |
| `Auth__Emails__{username}` | Where each user's password reset email goes | unset |

Secrets and per-deployment values (above) are never put in `appsettings.json`. Everything else is a setting whose default lives in `src/DenialsCommandCenter.Api/appsettings.json`; any of them can be overridden with a `Section__Key` environment variable (for example `Limits__MaxPageSize=200`). Settings are validated at startup, so a missing or out-of-range value stops the API with a message naming it instead of quietly switching a protection off.

| Setting | Default | Meaning |
|---|---|---|
| `Gemini:BaseUrl` | `https://generativelanguage.googleapis.com/` | Gemini API base URL |
| `Gemini:Model` | `gemini-2.5-flash` | Model used for drafting and evaluation |
| `Gemini:DailyCallLimit` | `200` | Most model calls per UTC day (0 turns AI calls off) |
| `Gemini:TimeoutSeconds` | `30` | Timeout for one model call |
| `Gemini:PauseAfterRefusalSeconds` | `60` | Pause after a 429/5xx that gives no retry delay |
| `Gemini:EvaluationStopAfterUnavailable` | `3` | The AI evaluation stops after this many unavailable answers in a row |
| `Graph:AuthorityUrl` | `https://login.microsoftonline.com/` | Microsoft identity platform for the mail token |
| `Graph:ApiBaseUrl` | `https://graph.microsoft.com/v1.0/` | Microsoft Graph base URL |
| `Graph:Scope` | `https://graph.microsoft.com/.default` | Token scope |
| `Graph:TimeoutSeconds` | `30` | Timeout for one Graph call |
| `Auth:SessionHours` | `8` | Sign-in session lifetime (sliding) |
| `Auth:SessionStampCacheSeconds` | `30` | How long a password or role change can take to end other sessions |
| `Auth:PasswordResetMinutes` | `30` | Lifetime of a password reset link |
| `Auth:PasswordMinLength` / `Auth:PasswordMaxLength` | `8` / `128` | Password length rules |
| `App:PublicUrl` | `http://127.0.0.1:3000` | Address of the web app, used in reset links |
| `Limits:LoginAttemptsPerMinute` | `10` | Sign-in and password reset attempts per client IP per minute |
| `Limits:ExpensiveConcurrentRequests` | `4` | AI, ingestion and review queue requests that run at once |
| `Limits:ExpensiveQueueLength` | `50` | Such requests waiting in line before 429 |
| `Limits:MaxRequestBodyBytes` | `65536` | Largest request body accepted |
| `Limits:DefaultPageSize` / `Limits:MaxPageSize` | `25` / `100` | Grid page size when none is asked for, and the largest allowed |
| `Limits:MaxNoteLength` | `2000` | Longest work item note |
| `Ingestion:DataDirectory` | `/data` | Directory holding the data pack (compose mounts `data/` there; set `Ingestion__DataDirectory` when running without Docker) |
| `Ingestion:Today` | `2026-09-30` | The "today" used for deadlines |

## Denial analysis
The rules engine decides the root cause, owning team, preventability, next action, citations and recoverability. It works without AI.

Gemini drafts appeal letters from de-identified facts only; patient and provider details are merged into the letter locally. Its output is validated and cached, and anything that fails validation, or any Gemini outage, falls back to the template letter.

| Endpoint | Purpose |
|---|---|
| `GET /api/denials/summary` | Open denials, denied amount, expected value by recovery bucket, and recoverable claims due within 14 and 30 days |
| `GET /api/denials?bucket=&payerId=&rootCause=` | Prioritised worklist |
| `GET /api/denials/{claimId}` | Analysis plus the cached letter draft for the claim's current facts |
| `POST /api/denials/{claimId}/draft` | Generate (or reuse) a letter draft with the merged letter |
| `GET /api/review-queue` | Low-confidence analyses, rejected AI drafts, and AI/rules root-cause disagreements |
| `GET /api/evaluation` | Rules engine scored against the 40 expert labels |
| `POST /api/evaluation/ai` | Scores the AI against the same labels and compares it with the rules (409 without `Gemini__ApiKey`) |

## Worklist
There are two roles. A Manager sees and changes every claim, is the only role that can reassign work, and has the review queue, exceptions and reconciliation pages. A Denials Specialist sees and changes only the claims assigned to them and gets 403 on everything else.

The queue is ordered by recovery bucket (recoverable, then maybe-recoverable for eligibility, then close-out), then priority score, then days to deadline, then denied amount. Closed items are hidden by default.

Expected value is the denied amount multiplied by the payer's observed allowed ratio. Urgency is a multiplier on the days left before the deadline: x3 when 14 days or fewer are left, x2 when 30 or fewer, otherwise x1. Priority score is expected value x urgency.

Every change to status, notes and assignment is written to the audit log in the same transaction as the change. Ingestion creates work items for new denials but never changes or deletes existing work items, notes or audit entries, so a forced re-ingestion keeps every status, assignee and note.

### Front end

React with TanStack Query for server state and daisyUI (Tailwind CSS) for every component; no other UI library. Each screen's components fetch their own data, a change refreshes only the data it affects (a note refreshes the notes, not the page), list pages keep the previous rows on screen while the next page loads, first loads show skeletons, and each page's code is downloaded only when it is first opened. Search, sorting and paging run in the API, with the page size capped by the API's `Limits:MaxPageSize` setting, so a screen costs the same at any data size.

## Scaling and caching

What is cached, where, and what a production deployment would use instead:

| What | Why | Here | Production |
|---|---|---|---|
| AI answers (appeal drafts, blind classifications) | Each model call costs money and the inputs fully determine the answer | Postgres `AppealDrafts`, keyed by SHA-256 of prompt version, instructions, schema, model, purpose, claim and de-identified facts. Any input change is a new key, so nothing goes stale and nothing needs invalidating. Accepted and rejected answers are kept; outages are not. | Same table as the durable record, with Redis in front for the hot path |
| Identical calls in flight | Two clicks, or two users on one claim, must not pay twice | In-process single-flight (`AiCallGuard`) | Redis `SET NX` lock per key so all API instances share it |
| Provider refusals (429/5xx) | Calling a provider that is out of quota wastes time and can extend the block | In-memory pause until Retry-After, or until the next UTC day when the daily quota is gone | Shared Redis key with expiry |
| Daily AI budget | Daily cap on spend; concurrent calls can go slightly over it because the count is checked before each call, not reserved atomically | Count of today's stored answers vs `Gemini__DailyCallLimit` | Redis `INCR` with a day expiry, plus provider-side budget alerts |
| Reference data (reason codes, policies) | Read on every claim page, changes only with a deploy | Singleton in the API, one-hour cache in the browser | Same, plus CDN/ETag |
| Lists and claim details | Fast screens without hammering the database | Browser cache with TanStack Query (15 to 60 s freshness, refetch only what a change affects) | Same, plus read replicas for list queries |

Reads never call the AI: the claim page shows the cached letter or the rule-based template, and only "Draft appeal letter" or the manager's evaluation run may call the model.

Work that should not run inside a web request in production: AI drafting and the AI evaluation would be queued (RabbitMQ or Azure Service Bus); a worker calls the model with retries and exponential backoff, failed jobs go to a dead-letter queue, and the screen is updated by polling or SignalR. Payer-file ingestion would use the same queue. The API stays stateless (session cookies with the Data Protection key ring in shared storage), so it scales horizontally behind the load balancer; the static web bundle is served from a CDN.

## Architecture

```
data/ (claims CSV, 835 remits, worklog XLSX, policies, rules)
   │  read-only mount
   ▼
API (.NET 10, ASP.NET Core minimal APIs)                         web (React 19 + TypeScript)
   Domain project: X12 835 parser, claim matcher,                   served by nginx, which also
   worklog normaliser, claim-state projection,                      proxies /api to the API
   rules engine, recoverability, appeal facts                       TanStack Query for server state,
   Api project: ingestion pipeline, EF Core,                        daisyUI on Tailwind for the UI
   endpoints, cookie auth, AI drafting and guard
   │
   ▼
PostgreSQL 17
   derived tables (rebuilt by each ingestion): claims, events, claim states, issues, analyses
   human tables (never touched by ingestion): work items, notes, audit log, users, AI answer cache
```

- **Ingestion** hashes all inputs. Unchanged inputs are a no-op.
  - Changed inputs rebuild the derived tables in one transaction under an advisory lock.
  - Each remittance event has a deterministic key, so a resent file cannot double-count a payment.
  - The rules engine then analyses every open denial and creates work items for new denials.
- **The API** checks every setting at startup.
  - Sessions are HttpOnly cookies that end when the user's password or role changes.
  - Specialists can reach only their own claims, and expensive endpoints are rate- and concurrency-limited.

## Key decisions and trade-offs

- **The rules engine is the source of truth; the AI drafts and gives a second opinion.**
  - Root cause, team, preventability, next action, deadlines and citations come from deterministic rules built from the payer policies. They are explainable and scored 40/40 on the expert labels.
  - Gemini writes the letter and independently classifies the denial. Its output is rejected if it cites a section that does not exist or disagrees with the rules, and disagreements go to the review queue. This keeps the money numbers correct even when the AI is wrong or down.
- **De-identified AI input.** The model never sees names, member IDs or dates of birth; they are merged into the letter locally after validation.
- **AI cost control:**
  - Answers are cached under a hash of all their inputs, so a repeated question costs nothing and nothing goes stale.
  - Identical in-flight calls share one call.
  - Calls pause after a quota refusal, and a daily cap applies.
  - Reads never call the AI. See [Scaling and caching](#scaling-and-caching).
- **Denial date** is the payment date of the remittance (BPR16), not the service date. Appeal windows run from that date.
- **Payer reversals** are matched to the payment they cancel by payer claim number. One that matches nothing is reported, not guessed.
- **Expected value** is the denied amount multiplied by the payer's historical paid-to-billed ratio. It is an estimate, labelled as such.
- **Priority** puts what can still be won first, then expected money, weighted by deadline urgency: ×3 at 14 days or fewer, ×2 at 30 or fewer.
- **Cookie sessions over JWT.** The app and API share an origin through nginx, so HttpOnly SameSite cookies avoid token storage in the browser and allow server-side revocation.
- **Server-side search, sorting and paging,** so screens cost the same at any data size.
- **Settings live in `appsettings.json`,** with secrets only in the environment. Every setting is validated at startup.

## What is not finished and why

The brief asks for correct parts over many loose ones, so B, C and D were finished and tested first. They answer the manager's questions and protect the money. Still open:

- **E. Manager analytics and prevention:**
  - Trends over time.
  - Breakdowns by payer, reason, provider, coder and facility. The data is all in the database: every analysis carries the payer, root cause and team, and every claim carries the provider, coder and facility. What is missing is the endpoints and the page.
  - The prevention view and its JSON rule export for the pre-bill team.
- **Screen recording and total hours:** to be provided with the submission.

## Known limitations
- A work item closed by staff stays closed if the payer later denies the claim again.
- Claims that become paid leave the queue without a system audit entry.
- Two people editing the same work item at once: the second save is refused with a conflict (409) and has to be retried. Every change is audited.
- Ingestion skips work when the data files are unchanged. After changing the analysis rules, run a forced re-ingestion (`POST /api/ingestion/run?force=true`, manager) so existing denials are re-analysed. A fresh database always computes them with the current rules.
- The AI answer cache, the single-flight guard and the quota pause live in one API process. Running several API instances would need Redis for them, as described above.
- The request-size limit is enforced by Kestrel. The tests check that the setting reaches Kestrel, because the in-memory test server does not enforce it.

## Time spent

_To be filled in: honest total hours._

## Tests
`dotnet test` (Docker must be running for the integration tests).

The `data/` folder is the fictional data pack from the assignment. It is committed so the repository runs with one command.
