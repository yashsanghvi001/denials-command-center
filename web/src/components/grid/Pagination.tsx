import { useEffect } from "react";
import { appConfig } from "../../lib/config";
import type { PagedResult } from "../../lib/types";
import { useGridParams } from "../../lib/useGridParams";
import { ChevronLeftIcon, ChevronRightIcon } from "../icons";

function visiblePages(current: number, total: number) {
  const pages: (number | "gap")[] = [];
  for (let page = 1; page <= total; page++) {
    if (page === 1 || page === total || Math.abs(page - current) <= 1) pages.push(page);
    else if (pages[pages.length - 1] !== "gap") pages.push("gap");
  }
  return pages;
}

export function Pagination({ result }: { result: PagedResult<unknown> }) {
  const { update } = useGridParams();
  const { page, pageSize, totalItems, totalPages } = result;
  const first = totalItems === 0 ? 0 : (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, totalItems);

  // A reassignment, status change or new filter can empty the current page; step back instead of showing nothing.
  useEffect(() => {
    if (totalPages > 0 && page > totalPages) update({ page: String(totalPages) });
  }, [page, totalPages, update]);

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-base-200 px-4 py-3 text-sm">
      <span className="text-base-content/70">
        Showing {first} to {last} of {totalItems}
      </span>
      <div className="flex items-center gap-3">
        <label className="flex items-center gap-2 text-base-content/70">
          Rows
          <select className="select select-sm w-20" value={pageSize} onChange={(e) => update({ pageSize: e.target.value })}>
            {appConfig.pageSizes.map((size) => (
              <option key={size} value={size}>{size}</option>
            ))}
          </select>
        </label>
        {totalPages > 1 && (
          <nav className="join" aria-label="Pages">
            <button type="button" className="join-item btn btn-sm" disabled={page <= 1} onClick={() => update({ page: String(page - 1) })} aria-label="Previous page">
              <ChevronLeftIcon />
            </button>
            {visiblePages(page, totalPages).map((item, index) =>
              item === "gap" ? (
                <button key={`gap-${index}`} type="button" className="join-item btn btn-sm btn-disabled" tabIndex={-1}>…</button>
              ) : (
                <button
                  key={item}
                  type="button"
                  className={`join-item btn btn-sm ${item === page ? "btn-primary" : ""}`}
                  aria-current={item === page ? "page" : undefined}
                  onClick={() => update({ page: String(item) })}
                >
                  {item}
                </button>
              ),
            )}
            <button type="button" className="join-item btn btn-sm" disabled={page >= totalPages} onClick={() => update({ page: String(page + 1) })} aria-label="Next page">
              <ChevronRightIcon />
            </button>
          </nav>
        )}
      </div>
    </div>
  );
}
