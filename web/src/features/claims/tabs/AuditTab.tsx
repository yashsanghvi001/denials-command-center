import { ErrorAlert } from "../../../components/Feedback";
import { SectionCard } from "../../../components/SectionCard";
import { TableSkeletonRows } from "../../../components/Skeletons";
import { formatDate, formatDateTime, formatMoney, severityBadge } from "../../../lib/format";
import { issueHint, issueLabel } from "../../../lib/labels";
import { useWorkItem } from "../../work-items/queries";
import { describeChange, personName } from "../claimText";
import { useClaim } from "../queries";

export function AuditTab({ claimId }: { claimId: string }) {
  const work = useWorkItem(claimId);
  const claim = useClaim(claimId);
  if (work.isError) return <ErrorAlert error={work.error} />;
  if (claim.isError) return <ErrorAlert error={claim.error} />;

  return (
    <div className="space-y-5">
      <SectionCard title="Change history" flush>
        <table className="table table-sm">
          <thead>
            <tr>
              <th>When</th>
              <th>Who</th>
              <th>What changed</th>
            </tr>
          </thead>
          <tbody>
            {work.isPending && <TableSkeletonRows rows={3} columns={3} />}
            {work.data === null && (
              <tr>
                <td colSpan={3} className="text-base-content/60">This claim is not on the worklist, so it has no change history.</td>
              </tr>
            )}
            {work.data?.audit.map((entry) => (
              <tr key={entry.id}>
                <td className="whitespace-nowrap">{formatDateTime(entry.at)}</td>
                <td className="whitespace-nowrap">{personName(entry.actor)}</td>
                <td>{describeChange(entry)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </SectionCard>
      {claim.data && claim.data.issues.length > 0 && (
        <SectionCard title="Data problems found on this claim" flush>
          <table className="table table-sm">
            <thead>
              <tr>
                <th>Severity</th>
                <th>Problem</th>
                <th className="text-right">Amount</th>
              </tr>
            </thead>
            <tbody>
              {claim.data.issues.map((issue) => (
                <tr key={issue.key}>
                  <td><span className={severityBadge(issue.severity)}>{issue.severity}</span></td>
                  <td>
                    <div className="font-medium">{issueLabel(issue.kind)}</div>
                    <div>{issue.reason}</div>
                    {issueHint(issue.kind) && <div className="text-xs text-base-content/60">What to do: {issueHint(issue.kind)}</div>}
                  </td>
                  <td className="text-right tabular-nums">{issue.amount === null ? "None" : formatMoney(issue.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </SectionCard>
      )}
      {claim.data && claim.data.worklog.length > 0 && (
        <SectionCard title="Entries from the old worklog spreadsheet" flush>
          <table className="table table-sm">
            <thead>
              <tr>
                <th>Logged</th>
                <th>Owner</th>
                <th>Status</th>
                <th>Note</th>
              </tr>
            </thead>
            <tbody>
              {claim.data.worklog.map((entry) => (
                <tr key={entry.rowNumber}>
                  <td className="whitespace-nowrap">{entry.loggedDate ? formatDate(entry.loggedDate) : "Unclear date"}</td>
                  <td>{entry.owner ?? "Nobody"}</td>
                  <td>{entry.statusRaw || "Not recorded"}</td>
                  <td>
                    {entry.notes}
                    {entry.suspiciousText && (
                      <div className="text-xs text-warning">This note contains instructions aimed at an AI system. It is shown as-is and never acted on.</div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </SectionCard>
      )}
    </div>
  );
}
