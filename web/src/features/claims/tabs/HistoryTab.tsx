import { ErrorAlert } from "../../../components/Feedback";
import { SectionCard } from "../../../components/SectionCard";
import { TableSkeletonRows } from "../../../components/Skeletons";
import { formatDate, formatMoney } from "../../../lib/format";
import { adjustmentGroupLabel, remitEventLabel } from "../../../lib/labels";
import { useClaim, useReference } from "../queries";

export function HistoryTab({ claimId }: { claimId: string }) {
  const { data, isPending, isError, error } = useClaim(claimId);
  const reference = useReference();
  if (isError) return <ErrorAlert error={error} />;

  return (
    <div className="space-y-5">
      <SectionCard title="History with the payer" flush>
        <table className="table table-sm">
          <thead>
            <tr>
              <th>Date</th>
              <th>What happened</th>
              <th className="text-right">Amount billed</th>
              <th className="text-right">Paid</th>
              <th>Source</th>
            </tr>
          </thead>
          <tbody>
            {isPending && <TableSkeletonRows rows={3} columns={5} />}
            {data && (
              <tr>
                <td className="whitespace-nowrap">{formatDate(data.claim.submittedDate)}</td>
                <td>Claim sent to the payer</td>
                <td className="text-right tabular-nums">{formatMoney(data.claim.totalCharge)}</td>
                <td className="text-right" />
                <td className="text-xs text-base-content/60">Claims export</td>
              </tr>
            )}
            {data?.events.map((event, index) => (
              <tr key={`${event.traceNumber}-${index}`}>
                <td className="whitespace-nowrap">{formatDate(event.paymentDate)}</td>
                <td>{remitEventLabel(event.statusCode)}</td>
                <td className="text-right tabular-nums">{formatMoney(event.charge)}</td>
                <td className="text-right tabular-nums">{formatMoney(event.paid)}</td>
                <td className="text-xs text-base-content/60">
                  Payer file {event.sourceFile}, payment {event.traceNumber}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </SectionCard>
      {data && data.denialLines.length > 0 && (
        <SectionCard title="What the payer denied" flush>
          <table className="table table-sm">
            <thead>
              <tr>
                <th>Procedure</th>
                <th>Why the payer denied it</th>
                <th>Payer's remark</th>
                <th className="text-right">Amount</th>
              </tr>
            </thead>
            <tbody>
              {data.denialLines.map((line, index) => (
                <tr key={index}>
                  <td>{line.procedureCode}</td>
                  <td>
                    <div>{reference.data?.reasonCodes[line.reason] ?? `Reason code ${line.reason}`}</div>
                    <div className="text-xs text-base-content/60">
                      {line.group}-{line.reason} · {adjustmentGroupLabel(line.group)}
                    </div>
                  </td>
                  <td>
                    {line.remarks.length === 0 && "None"}
                    {line.remarks.map((remark) => (
                      <div key={remark}>
                        {reference.data?.remarkCodes[remark] ?? "Remark not in our reference list."} <span className="text-xs text-base-content/60">({remark})</span>
                      </div>
                    ))}
                  </td>
                  <td className="text-right tabular-nums">{formatMoney(line.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </SectionCard>
      )}
    </div>
  );
}
