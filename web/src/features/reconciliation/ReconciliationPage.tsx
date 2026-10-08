import { useQuery } from "@tanstack/react-query";
import { ErrorAlert } from "../../components/Feedback";
import { PageHeader } from "../../components/PageHeader";
import { SectionCard } from "../../components/SectionCard";
import { StatsSkeleton } from "../../components/Skeletons";
import { api } from "../../lib/api";
import { appConfig } from "../../lib/config";
import { formatMoney, splitWords } from "../../lib/format";
import type { Reconciliation } from "../../lib/types";

export function ReconciliationPage() {
  const { data, isPending, isError, error } = useQuery({
    queryKey: ["reconciliation"],
    queryFn: () => api.get<Reconciliation>("/api/reconciliation"),
    staleTime: appConfig.freshness.lists,
  });

  if (isPending) {
    return (
      <div className="space-y-5">
        <div className="skeleton h-8 w-64" />
        <StatsSkeleton count={4} />
      </div>
    );
  }
  if (isError && !data) {
    return (
      <div className="space-y-5">
        <PageHeader title="Reconciliation" />
        <ErrorAlert error={error} />
      </div>
    );
  }

  const balanced = data.difference === 0;
  return (
    <div className="space-y-5">
      <PageHeader title="Reconciliation" subtitle="Every dollar in the payer files is accounted for: paid to our claims, paid to claims we could not match, or adjusted at provider level." />
      {isError && <ErrorAlert error={error} />}
      <div className="stats stats-vertical w-full bg-base-100 shadow-sm lg:stats-horizontal">
        <div className="stat">
          <div className="stat-title">Payer files total</div>
          <div className="stat-value text-2xl">{formatMoney(data.payerFilesTotal)}</div>
          <div className="stat-desc">{formatMoney(data.duplicatePaymentsExcluded)} in resent duplicates excluded</div>
        </div>
        <div className="stat">
          <div className="stat-title">Paid to our claims</div>
          <div className="stat-value text-2xl">{formatMoney(data.paidToKnownClaims)}</div>
        </div>
        <div className="stat">
          <div className="stat-title">Paid to claims we could not match</div>
          <div className="stat-value text-2xl">{formatMoney(data.paidToUnmatched)}</div>
          <div className="stat-desc">Provider-level adjustments {formatMoney(data.providerLevelAdjustments)}</div>
        </div>
        <div className="stat">
          <div className="stat-title">Difference</div>
          <div className={`stat-value text-2xl ${balanced ? "text-success" : "text-error"}`}>{formatMoney(data.difference)}</div>
          <div className="stat-desc">{balanced ? "Fully reconciled" : "Needs investigation"}</div>
        </div>
      </div>
      <div className="grid gap-5 xl:grid-cols-3">
        <div className="xl:col-span-2">
          <SectionCard title="Remittance files" flush>
            <table className="table table-sm">
              <thead>
                <tr>
                  <th>File</th>
                  <th className="text-right">Claim payments</th>
                  <th className="text-right">New</th>
                  <th className="text-right">Duplicates</th>
                  <th className="text-right">Payment total</th>
                  <th className="text-right">Counted</th>
                  <th>Outcome</th>
                </tr>
              </thead>
              <tbody>
                {data.files.map((file) => (
                  <tr key={file.fileName}>
                    <td className="whitespace-nowrap">{file.fileName}</td>
                    <td className="text-right">{file.claimPayments}</td>
                    <td className="text-right">{file.newEvents}</td>
                    <td className="text-right">{file.duplicateEvents}</td>
                    <td className="text-right tabular-nums">{formatMoney(file.paymentTotal)}</td>
                    <td className="text-right tabular-nums">{formatMoney(file.newPaymentTotal)}</td>
                    <td>{file.outcome}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </SectionCard>
        </div>
        <SectionCard title="Claims by status" flush>
          <table className="table table-sm">
            <tbody>
              {Object.entries(data.claimsByStatus).map(([status, count]) => (
                <tr key={status}>
                  <td>{splitWords(status)}</td>
                  <td className="text-right">{count}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </SectionCard>
      </div>
    </div>
  );
}
