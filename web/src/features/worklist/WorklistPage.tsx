import { PageHeader } from "../../components/PageHeader";
import { useSignedInUser } from "../auth/useAuth";
import { MoneySummary } from "./MoneySummary";
import { WorklistFilters } from "./WorklistFilters";
import { WorklistTable } from "./WorklistTable";

export function WorklistPage() {
  const isManager = useSignedInUser().role === "Manager";

  return (
    <div className="space-y-5">
      <PageHeader title={isManager ? "All denials" : "My worklist"} subtitle="The claims most worth working today are at the top." />
      {isManager && <MoneySummary />}
      <details className="collapse-arrow collapse rounded-box border border-base-300 bg-base-100 text-sm">
        <summary className="collapse-title min-h-0 py-3 font-medium">How is this order decided?</summary>
        <div className="collapse-content space-y-2 text-base-content/80">
          <ol className="list-decimal space-y-1 pl-5">
            <li>Claims we can still get paid on.</li>
            <li>Claims that depend on whether the patient had other insurance.</li>
            <li>Claims to close out: the appeal deadline has passed or the payer's policy does not allow payment.</li>
          </ol>
          <p>
            Within each group the highest priority comes first. Priority is the amount we expect back, tripled when 14 days or fewer are left
            before the payer's deadline and doubled when 30 days or fewer are left. Click a column heading to sort by that column instead.
          </p>
        </div>
      </details>
      <WorklistFilters />
      <WorklistTable />
    </div>
  );
}
