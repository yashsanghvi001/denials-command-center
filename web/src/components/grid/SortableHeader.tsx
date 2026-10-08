import { useGridParams } from "../../lib/useGridParams";

export function SortableHeader({ column, label, numeric }: { column: string; label: string; numeric?: boolean }) {
  const grid = useGridParams();
  const active = grid.sort === column;
  const descending = active && grid.direction === "desc";

  return (
    <th scope="col" className={numeric ? "text-right" : undefined} aria-sort={active ? (descending ? "descending" : "ascending") : "none"}>
      <button
        type="button"
        className={`inline-flex items-center gap-1 whitespace-nowrap font-semibold hover:text-primary ${numeric ? "flex-row-reverse" : ""}`}
        onClick={() => grid.update({ sort: column, direction: active && !descending ? "desc" : "asc" })}
      >
        {label}
        <span aria-hidden="true" className={active ? "text-primary" : "opacity-0"}>{descending ? "▼" : "▲"}</span>
      </button>
    </th>
  );
}
