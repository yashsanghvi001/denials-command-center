import { Detail, DetailList } from "../../../components/DetailList";
import { ErrorAlert } from "../../../components/Feedback";
import { SectionCard } from "../../../components/SectionCard";
import { DetailSkeleton } from "../../../components/Skeletons";
import { formatDate, formatMoney, splitWords } from "../../../lib/format";
import { useClaim } from "../queries";

export function OverviewTab({ claimId }: { claimId: string }) {
  const { data, isPending, isError, error } = useClaim(claimId);
  if (isPending) return <SectionCard title="Claim details"><DetailSkeleton lines={8} /></SectionCard>;
  if (isError) return <ErrorAlert error={error} />;

  const { claim, state, lines } = data;
  return (
    <div className="grid gap-5 xl:grid-cols-2">
      <SectionCard title="Claim details">
        <DetailList>
          <Detail label="Payer">{claim.payer}</Detail>
          <Detail label="Date of service">{formatDate(claim.dateOfService)}</Detail>
          <Detail label="Billed on">{formatDate(claim.submittedDate)}</Detail>
          <Detail label="Provider">{claim.renderingProvider}</Detail>
          <Detail label="Facility">{claim.facility}</Detail>
          <Detail label="Coder">
            {claim.coderId}, {claim.prebillReviewed ? "reviewed before billing" : "not reviewed before billing"}
          </Detail>
        </DetailList>
      </SectionCard>
      <SectionCard title="Money">
        <DetailList>
          <Detail label="Billed amount">{formatMoney(state.billedCharge)}</Detail>
          <Detail label="Paid so far">{formatMoney(state.netPaid)}</Detail>
          <Detail label="Denied">{formatMoney(state.deniedAmount)}</Detail>
          <Detail label="Claim status">
            {splitWords(state.status)}
            {state.wasRecouped && " (the payer took money back)"}
          </Detail>
          {state.possibleOverpayment > 0 && (
            <Detail label="Possible overpayment">
              <span className="badge badge-soft badge-warning">{formatMoney(state.possibleOverpayment)}</span> the payer may take back
            </Detail>
          )}
        </DetailList>
      </SectionCard>
      <div className="xl:col-span-2">
        <SectionCard title="What was billed" flush>
          <table className="table table-sm">
            <thead>
              <tr>
                <th>Line</th>
                <th>Procedure (CPT)</th>
                <th>Modifier</th>
                <th className="text-right">Units</th>
                <th className="text-right">Charge</th>
                <th>Authorization</th>
                <th>Diagnosis codes</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line) => (
                <tr key={line.lineNo}>
                  <td>{line.lineNo}</td>
                  <td>{line.cpt}</td>
                  <td>{line.modifier || "None"}</td>
                  <td className="text-right">{line.units}</td>
                  <td className="text-right tabular-nums">{formatMoney(line.charge)}</td>
                  <td>{line.authNumber || "None"}</td>
                  <td>{line.diagnosisCodes.join(", ")}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </SectionCard>
      </div>
    </div>
  );
}
