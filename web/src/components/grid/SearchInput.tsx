import { useEffect, useRef, useState } from "react";
import { appConfig } from "../../lib/config";
import { useGridParams } from "../../lib/useGridParams";
import { SearchIcon } from "../icons";

export function SearchInput({ label, placeholder }: { label: string; placeholder: string }) {
  const { params, update } = useGridParams();
  const value = params.get("search") ?? "";
  const [text, setText] = useState(value);
  const [lastValue, setLastValue] = useState(value);
  const timer = useRef<number | undefined>(undefined);

  if (value !== lastValue) {
    setLastValue(value);
    setText(value);
  }

  useEffect(() => () => window.clearTimeout(timer.current), []);

  function change(next: string) {
    setText(next);
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => update({ search: next }), appConfig.searchDebounceMs);
  }

  return (
    <label className="input w-full">
      <SearchIcon className="opacity-50" />
      <input type="search" className="grow" placeholder={placeholder} aria-label={label} value={text} onChange={(e) => change(e.target.value)} />
    </label>
  );
}
