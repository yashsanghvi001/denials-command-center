import { splitWords } from "./format";

const bucketLabels: Record<string, string> = {
  Recoverable: "Can be recovered",
  MaybeEligibility: "Check other coverage",
  LostWindowExpired: "Deadline passed",
  LostPolicy: "Policy write-off",
};

const workStatusLabels: Record<string, string> = {
  Open: "Not started",
  InProgress: "In progress",
  PendingPayer: "Waiting on payer",
  Closed: "Closed",
};

const issueKinds: Record<string, { label: string; hint: string }> = {
  DuplicateFile: { label: "Same file received twice", hint: "Nothing to do; the copy was skipped." },
  UnparseableFile: { label: "File could not be read", hint: "Ask the clearinghouse to resend the file." },
  DuplicateRemittanceFile: { label: "Payer resent a remittance file", hint: "Nothing to do; payments already loaded were not counted twice." },
  PartialDuplicateTransaction: { label: "Payment partly repeats an earlier one", hint: "Confirm with the payer which claim payments are new." },
  TransactionOutOfBalance: { label: "Payment total does not add up", hint: "Compare the check or EFT total with the claim payments in it." },
  ClaimOutOfBalance: { label: "Claim payment does not add up", hint: "Review the payer's adjustments on this claim." },
  UnmatchedRemittance: { label: "Payment not matched to any of our claims", hint: "Likely another practice's money. Contact the payer to return or redirect it." },
  MemberMismatch: { label: "Patient on payment does not match the claim", hint: "Payment is on hold. Confirm the member ID with the payer." },
  UnknownClaimStatus: { label: "Unrecognized claim status", hint: "Review the remittance for this claim by hand." },
  PossibleOverpayment: { label: "Payer may take money back", hint: "Do not count the extra as revenue; expect a recoupment." },
  UnmatchedReversal: { label: "Payer reversal with no matching payment", hint: "Check the payer claim number on the reversal." },
  ClaimsRowInvalid: { label: "Bad row in the claims export", hint: "Fix the row in the billing system and export again." },
  ClaimsHeaderConflict: { label: "Claim lines disagree with each other", hint: "Lines of the same claim carry different claim details; check the export." },
  ClaimsDuplicateLine: { label: "Claim line listed twice", hint: "Nothing to do; the second copy was ignored." },
  WorklogUnmatchedClaim: { label: "Worklog row for an unknown claim", hint: "Correct the claim number in the worklog." },
  WorklogDuplicateRow: { label: "Worklog row entered twice", hint: "Nothing to do; the copy was ignored." },
  WorklogAmbiguousDate: { label: "Unclear date in the worklog", hint: "Day and month could be swapped. Confirm the date with the owner." },
  WorklogUnparseableDate: { label: "Unreadable date in the worklog", hint: "Correct the date in the worklog." },
  WorklogSuspiciousText: { label: "Suspicious text in a worklog note", hint: "Nothing to do; the text is shown as-is and never acted on." },
  WorklogClosedButStillDenied: { label: "Closed in the worklog, but still denied", hint: "Money may have been given up by mistake. Reopen and work the denial." },
  WorklogOpenButPaid: { label: "Open in the worklog, but already paid", hint: "Close the worklog item." },
  WorklogAmountMismatch: { label: "Worklog amount differs from the denial", hint: "Use the payer's denied amount." },
};

const adjustmentGroups: Record<string, string> = {
  CO: "Contractual obligation (patient cannot be billed)",
  PR: "Patient responsibility",
  OA: "Other adjustment",
  PI: "Payer-initiated reduction",
};

const remitEvents: Record<string, string> = {
  "1": "Paid",
  "2": "Paid as secondary",
  "3": "Paid as tertiary",
  "4": "Denied",
  "22": "Payer reversed an earlier payment",
};

export const bucketLabel = (bucket: string) => bucketLabels[bucket] ?? splitWords(bucket);

export const workStatusLabel = (status: string) => workStatusLabels[status] ?? splitWords(status);

export const issueLabel = (kind: string) => issueKinds[kind]?.label ?? splitWords(kind);

export const issueHint = (kind: string) => issueKinds[kind]?.hint;

export const adjustmentGroupLabel = (group: string) => adjustmentGroups[group] ?? group;

export const remitEventLabel = (statusCode: string) => remitEvents[statusCode] ?? `Status code ${statusCode}`;

export function issueLocation(source: string, reference: string) {
  if (source === "remits") return "Remittance files";
  if (reference === source) return source;
  if (reference.includes("/")) return `${source}, payment ${reference.split("/")[0]}`;
  return `${source}, ${reference}`;
}
