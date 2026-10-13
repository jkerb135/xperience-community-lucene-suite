import { createContext, useContext, useEffect, useRef } from "react";
import { MenuItem, Pagination, Select, Spacing } from "@kentico/xperience-admin-components";
import { pageRange, rangeText } from "../rules/listing.ts";
import { TONE_COLOR } from "./dashboard.tsx";
import { barRatio, sparkPoints, type Tone } from "./report.ts";

/** True while the data on screen is the last loaded one after a failed request: graphics fade, text keeps its contrast. */
export const Stale = createContext(false);
export const STALE_OPACITY = 0.6;

export type BarTone = Tone | "product" | "info";

const BAR_COLOR: Record<BarTone, string> = { ...TONE_COLOR, product: "var(--color-product)", info: "var(--color-info-icon)" };

/** A number with a small bar of its share; the bar is decorative, the number is the readable text. */
export const DataBar = ({ value, ratio, tone, width = 72 }: { value: string; ratio: number; tone: BarTone; width?: number }) => {
  const stale = useContext(Stale);
  return (
    <span style={{ display: "flex", alignItems: "center", gap: Spacing.S, minWidth: 0, whiteSpace: "nowrap" }}>
      <span style={{ minWidth: 44, flexShrink: 0, textAlign: "right", fontVariantNumeric: "tabular-nums", color: "var(--color-text-default-on-light)" }}>{value}</span>
      <span
        aria-hidden="true"
        style={{ flex: `0 1 ${width}px`, minWidth: Spacing.L, height: 6, borderRadius: 3, overflow: "hidden", background: "var(--color-background-highlighted)" }}
      >
        <span style={{ display: "block", height: "100%", width: `${barRatio(ratio) * 100}%`, borderRadius: 3, background: BAR_COLOR[tone], opacity: stale ? STALE_OPACITY : 1 }} />
      </span>
    </span>
  );
};

const SPARK_WIDTH = 64;
const SPARK_HEIGHT = 22;

/** Decorative 64×22 trend line; `color` is a token (white on the selected tile). */
export const Sparkline = ({ values, color }: { values: readonly (number | null)[]; color: string }) => {
  const stale = useContext(Stale);
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      width={SPARK_WIDTH}
      height={SPARK_HEIGHT}
      style={{ display: "block", flexShrink: 0, overflow: "visible", opacity: stale ? STALE_OPACITY : 1 }}
    >
      {sparkPoints(values, SPARK_WIDTH, SPARK_HEIGHT).map((points) => (
        <polyline key={points} points={points} style={{ fill: "none", stroke: color, strokeWidth: 1.5, strokeLinejoin: "round", strokeLinecap: "round" }} />
      ))}
    </svg>
  );
};

export interface RangeFooterProps {
  readonly page: number;
  readonly pageSize: number;
  readonly total: number;
  readonly noun: string;
  readonly sizes: readonly number[];
  /** Accessible name of the page-size Select. */
  readonly sizeLabel: string;
  readonly onPageSize: (size: number) => void;
  readonly onPage: (page: number) => void;
  /** Most pages offered (e.g. the search result window); the range text still shows the real total. */
  readonly pageLimit?: number;
  /** Changes after a failed request; Kentico's Select keeps a picked size its `value` never confirmed, so it is remounted. */
  readonly resetKey?: number;
}

/** "1–25 of 1,240 queries", a page-size Select and Pagination (when there is more than one page). */
export const RangeFooter = ({ page, pageSize, total, noun, sizes, sizeLabel, onPageSize, onPage, pageLimit = Infinity, resetKey }: RangeFooterProps) => {
  const sizeRef = useRef<HTMLInputElement>(null);
  // Kentico's Select takes no aria-label prop; it is set on the native input.
  useEffect(() => {
    sizeRef.current?.setAttribute("aria-label", sizeLabel);
  });
  const { from, to } = pageRange(page, pageSize, total);
  const totalPages = Math.max(1, Math.min(Math.ceil(total / pageSize), pageLimit));
  return (
    <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", justifyContent: "space-between", gap: Spacing.L }}>
      <span style={{ color: "var(--color-text-default-on-light)", fontVariantNumeric: "tabular-nums" }}>{rangeText(from, to, total, noun)}</span>
      <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: Spacing.L }}>
        <div style={{ width: "140px" }}>
          <Select key={resetKey} inputRef={sizeRef} value={String(pageSize)} onChange={(value) => value && onPageSize(Number(value))}>
            {sizes.map((size) => (
              <MenuItem key={size} primaryLabel={`${size} per page`} value={String(size)} selected={size === pageSize} />
            ))}
          </Select>
        </div>
        {totalPages > 1 && <Pagination selectedPage={page} totalPages={totalPages} onPageChange={onPage} />}
      </div>
    </div>
  );
};
