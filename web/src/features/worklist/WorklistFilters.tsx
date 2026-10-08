import { SearchInput } from "../../components/grid/SearchInput";
import { formatMoney } from "../../lib/format";
import { useGridParams } from "../../lib/useGridParams";
import { useSignedInUser } from "../auth/useAuth";
import { useSpecialists } from "../work-items/queries";
import { useWorklist } from "./queries";

const statusFilters = [
  { value: "", label: "All open work" },
  { value: "Open", label: "Not started" },
  { value: "InProgress", label: "In progress" },
  { value: "PendingPayer", label: "Waiting on payer" },
  { value: "Closed", label: "Closed" },
  { value: "all", label: "Everything" },
];

export function WorklistFilters() {
  const isManager = useSignedInUser().role === "Manager";
  const { params, update } = useGridParams();
  const specialists = useSpecialists(isManager).data ?? [];
  const { data } = useWorklist();

  return (
    <div className="flex flex-col gap-3 md:flex-row md:items-center">
      <div className="md:w-96">
        <SearchInput label="Search claims" placeholder="Claim, patient, payer or denial cause" />
      </div>
      <select className="select md:w-48" aria-label="Work status" value={params.get("status") ?? ""} onChange={(e) => update({ status: e.target.value })}>
        {statusFilters.map((filter) => (
          <option key={filter.value} value={filter.value}>{filter.label}</option>
        ))}
      </select>
      {isManager && (
        <select className="select md:w-48" aria-label="Assigned to" value={params.get("assignee") ?? ""} onChange={(e) => update({ assignee: e.target.value })}>
          <option value="">Everyone</option>
          <option value="unassigned">Nobody yet</option>
          {specialists.map((specialist) => (
            <option key={specialist.username} value={specialist.username}>{specialist.displayName}</option>
          ))}
        </select>
      )}
      {data && (
        <p className="text-sm md:ml-auto">
          <strong>{data.totalItems}</strong> claims · <strong>{formatMoney(data.totalDenied)}</strong> denied
        </p>
      )}
    </div>
  );
}
