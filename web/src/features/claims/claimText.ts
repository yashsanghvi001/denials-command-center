import type { AuditEntry, DraftResult, Reference } from "../../lib/types";
import { splitWords } from "../../lib/format";
import { workStatusLabel } from "../../lib/labels";

const preventableTexts: Record<string, string> = {
  Yes: "Yes, it could have been caught before billing",
  No: "No, the payer or circumstances caused it",
  Unknown: "Not sure",
};

export const preventableText = (value: string) => preventableTexts[value] ?? value;

export const personName = (username: string | null | undefined) =>
  username ? username.charAt(0).toUpperCase() + username.slice(1) : "nobody";

function readJson(json: string | null): Record<string, string | null> {
  if (!json) return {};
  try {
    return JSON.parse(json) as Record<string, string | null>;
  } catch {
    return {};
  }
}

export function describeChange(entry: AuditEntry) {
  const before = readJson(entry.beforeJson);
  const after = readJson(entry.afterJson);
  switch (entry.action) {
    case "Created":
      return `Added to the worklist as "${workStatusLabel(after.status ?? "Open")}", assigned to ${personName(after.assignedTo)}.`;
    case "StatusChanged":
      return `Work status changed from "${workStatusLabel(before.status ?? "")}" to "${workStatusLabel(after.status ?? "")}".`;
    case "Reassigned":
      return `Reassigned from ${personName(before.assignedTo)} to ${personName(after.assignedTo)}.`;
    case "NoteAdded":
      return `Added a note: "${after.text ?? ""}"`;
    case "LetterDrafted":
      return after.source === "Gemini" ? "Drafted an appeal letter with AI." : "Drafted an appeal letter from the standard template.";
    default:
      return splitWords(entry.action);
  }
}

export function draftCaption(draft: DraftResult) {
  switch (draft.status) {
    case "Accepted":
      return "Drafted by AI (Gemini) and checked against the payer policy and our rules.";
    case "Template":
      return "Standard letter built from our rules. AI drafting is not switched on.";
    case "Unavailable":
      return draft.statusReason ?? "Standard letter built from our rules. The AI service could not be reached.";
    case "Rejected":
      return `The AI draft did not pass our checks${draft.statusReason ? ` (${draft.statusReason})` : ""}${draft.letter ? ", so the standard letter is shown instead" : ""}.`;
    default:
      return draft.statusReason ?? "No letter is needed for this claim.";
  }
}

export function citationLabel(citation: string, reference: Reference | undefined) {
  const section = reference?.policySections[citation];
  const number = citation.split("§")[1]?.trim();
  return section ? { title: `${section.policyTitle}, section ${number}`, text: section.text } : { title: citation, text: "" };
}
