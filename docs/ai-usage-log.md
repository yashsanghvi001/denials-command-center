# AI usage log

Two kinds of AI were involved: an AI coding assistant that helped me build the product, and Gemini inside the product, which drafts appeal letters and gives a second opinion on each denial. Every line of code went through my review and the test suite before it was kept.

## Where AI helped most

- **Speed on well-specified plumbing.** It helped most with the X12 835 parser, the EF Core schema and migrations, the minimal API endpoints and the React screens. These are areas where the requirement is clear and the work is mostly typing.
- **Tests.** It wrote most of the 313 automated tests from the scenarios I described. These include golden tests that pin the real totals ($31,145 open, a $0.00 reconciliation difference, 40/40 on the expert labels), so any regression in the money numbers fails the build.
- **Independent review.** I had the work reviewed in small slices against the written design, and those reviews caught real bugs before they shipped:
  - parser exceptions that echoed patient data into logs
  - a validator that could be bypassed
  - the AI cache returning another claim's letter
  - a race on concurrent inserts
  - stale letters after re-ingestion
- **Turning policy text into rules.** It helped turn the five payer policy excerpts into explicit, cited rules, which I then checked line by line against the policies.

## Where it was wrong, and how I caught it

| What went wrong | How I caught it | Fix |
|---|---|---|
| Took the denial date from the service-date segment (DTM*050). Every appeal deadline was wrong. | Hand-checked totals from the raw 835 files did not match | Use the ERA payment date (BPR16); golden tests now pin the buckets |
| Kept calling Gemini after the free quota was used up, and every call returned 429 | Read the API logs during the AI evaluation run | Refusals are classified, calls pause until the quota resets, a daily budget caps spend, and identical calls share one request |
| Sign-in and sign-out cleared the data cache in a way that could leave the screen on the previous user | Code review of the sign-in flow | Write the user first, then drop only the other cached data |
| Claim tabs replaced browser history, so Back left the claim | Review against the design ("links and Back work") | Each tab change adds a history entry |
| Grey badges were invisible in the dark theme | My own testing in dark mode | Theme-safe badge helper, plus a build check that rejects theme-breaking colours |
| URLs, limits and timeouts hard-coded in the files that used them | My code review | Moved into `appsettings.json` with typed options that are validated at startup |
| Expected value still shown on written-off claims | The README totals did not add up | The rules were right but the stored analyses were stale; documented that a forced re-ingest is needed after a rules change |
| Password-reset request would have failed on the empty 202 response | Review of the API client | Parse a response body only when one is present |

## Guardrails I kept on the AI in the product

- **Rules decide, the AI assists.** The rules engine decides cause, team, preventability, deadline and money. Gemini never changes a number.
- **De-identified input.** Gemini sees de-identified facts only: no names, member IDs or dates of birth. A test asserts this.
- **Validated output:**
  - A draft is rejected if it cites a policy section that does not exist, or if it disagrees with the rules.
  - Disagreements go to the human review queue.
  - When the AI is unavailable, the standard template letter is used.
- **Untrusted text.** Text in the files is treated as untrusted. A worklog note written as instructions to an AI is flagged and shown as plain text, never followed.
