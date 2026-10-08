import { useCallback } from "react";
import { useSearchParams } from "react-router-dom";

export interface GridParams {
  params: URLSearchParams;
  sort: string;
  direction: string;
  update: (changes: Record<string, string>) => void;
}

export function useGridParams(): GridParams {
  const [params, setParams] = useSearchParams();

  const update = useCallback(
    (changes: Record<string, string>) => {
      setParams((current) => {
        const next = new URLSearchParams(current);
        for (const [key, value] of Object.entries(changes)) {
          if (value) next.set(key, value);
          else next.delete(key);
        }
        if (!("page" in changes)) next.delete("page");
        return next;
      });
    },
    [setParams],
  );

  return { params, sort: params.get("sort") ?? "", direction: params.get("direction") ?? "", update };
}
