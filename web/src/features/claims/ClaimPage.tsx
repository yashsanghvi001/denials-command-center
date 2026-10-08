import {
  Link,
  useLocation,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { ArrowLeftIcon } from "../../components/icons";
import { useSignedInUser } from "../auth/useAuth";
import { ClaimHeader } from "./ClaimHeader";
import { useClaim } from "./queries";
import { AuditTab } from "./tabs/AuditTab";
import { DenialTab } from "./tabs/DenialTab";
import { HistoryTab } from "./tabs/HistoryTab";
import { OverviewTab } from "./tabs/OverviewTab";
import { WorkTab } from "./tabs/WorkTab";

const tabs = [
  { id: "overview", label: "Overview" },
  { id: "denial", label: "Why denied" },
  { id: "history", label: "Payer history" },
  { id: "work", label: "Work & notes" },
  { id: "audit", label: "Audit" },
] as const;

type TabId = (typeof tabs)[number]["id"];

export function ClaimPage() {
  const { claimId = "" } = useParams();
  return <ClaimView key={claimId} claimId={claimId} />;
}

function ClaimView({ claimId }: { claimId: string }) {
  const isManager = useSignedInUser().role === "Manager";
  const [params, setParams] = useSearchParams();
  const claim = useClaim(claimId);
  const worklistSearch =
    (useLocation().state as { from?: string } | null)?.from ?? "";
  const requested = tabs.find((tab) => tab.id === params.get("tab"))?.id;
  const active: TabId = requested ?? (isManager ? "overview" : "work");

  function select(tab: TabId) {
    setParams((current) => {
      const next = new URLSearchParams(current);
      next.set("tab", tab);
      return next;
    });
  }

  return (
    <div className="space-y-5">
      <Link
        to={`/${worklistSearch}`}
        className="btn btn-ghost btn-sm -ml-2 gap-1"
      >
        <ArrowLeftIcon /> Back to worklist
      </Link>
      <ClaimHeader claimId={claimId} />
      {!claim.isError && (
        <>
          <div
            role="tablist"
            aria-label="Claim sections"
            className="tabs tabs-border flex-nowrap overflow-x-auto"
          >
            {tabs.map((tab) => (
              <button
                key={tab.id}
                type="button"
                role="tab"
                id={`tab-${tab.id}`}
                aria-selected={tab.id === active}
                aria-controls="claim-tab-panel"
                className={`tab whitespace-nowrap ${tab.id === active ? "tab-active font-medium" : ""}`}
                onClick={() => select(tab.id)}
              >
                {tab.label}
              </button>
            ))}
          </div>
          <div
            role="tabpanel"
            id="claim-tab-panel"
            aria-labelledby={`tab-${active}`}
          >
            {active === "overview" && <OverviewTab claimId={claimId} />}
            {active === "denial" && <DenialTab claimId={claimId} />}
            {active === "history" && <HistoryTab claimId={claimId} />}
            {active === "work" && <WorkTab claimId={claimId} />}
            {active === "audit" && <AuditTab claimId={claimId} />}
          </div>
        </>
      )}
    </div>
  );
}
