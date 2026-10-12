import { useEffect, useId, useRef, useState } from "react";
import { Spacing } from "@kentico/xperience-admin-components";
import { labelEvery, niceTicks } from "./report.ts";

export interface Bar {
  readonly label: string;
  readonly value: number;
  /** Drawn over the bar from the baseline (a subset of value, e.g. no-result searches). */
  readonly overlay?: number;
  /** Not final yet: drawn hatched and listed in the legend. */
  readonly muted?: boolean;
}

interface BarChartProps {
  readonly bars: readonly Bar[];
  /** Screen-reader summary; the chart itself is an image. */
  readonly summary: string;
  readonly valueLabel: string;
  readonly overlayLabel?: string;
  readonly mutedLabel?: string;
  readonly formatValue: (value: number) => string;
}

const HEIGHT = 220;
const LEFT = 56;
const BOTTOM = 28;
const TOP = 8;
const RIGHT = 8;

// Only tokens the 31.9.1 admin runtime defines (the kit repo has newer ones, e.g. --color-background-primary, that resolve to nothing here).
const BAR = "var(--color-icon-highlighted)";
const MUTED = "var(--color-background-selected)";
const text = { fill: "var(--color-text-low-emphasis)", fontSize: "smaller" };

/** Column chart in kit tokens only; pair it with a table view for exact values. */
export const BarChart = ({ bars, summary, valueLabel, overlayLabel, mutedLabel, formatValue }: BarChartProps) => {
  const container = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(640);
  const hatch = `hatch-${useId().replace(/:/g, "")}`;

  useEffect(() => {
    const element = container.current;
    if (!element) {
      return;
    }
    const observer = new ResizeObserver(([entry]) => setWidth(Math.max(240, Math.floor(entry.contentRect.width))));
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  const ticks = niceTicks(Math.max(0, ...bars.map((bar) => bar.value)));
  const top = ticks[ticks.length - 1];
  const plotWidth = width - LEFT - RIGHT;
  const plotHeight = HEIGHT - TOP - BOTTOM;
  const band = plotWidth / Math.max(1, bars.length);
  const barWidth = Math.max(1, band * 0.7);
  const y = (value: number) => TOP + plotHeight - (value / top) * plotHeight;
  const every = labelEvery(bars.length);
  const hasMuted = bars.some((bar) => bar.muted);

  return (
    <div ref={container}>
      <svg role="img" aria-label={summary} width={width} height={HEIGHT} style={{ display: "block" }}>
        <defs>
          <pattern id={hatch} width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
            <rect width="6" height="6" style={{ fill: MUTED }} />
            <line x1="0" y1="0" x2="0" y2="6" style={{ stroke: BAR, strokeWidth: 2 }} />
          </pattern>
        </defs>
        {ticks.map((tick) => (
          <g key={tick}>
            <line x1={LEFT} x2={width - RIGHT} y1={y(tick)} y2={y(tick)} style={{ stroke: "var(--color-border-disabled)" }} />
            <text x={LEFT - 6} y={y(tick)} textAnchor="end" dominantBaseline="middle" style={text}>
              {formatValue(tick)}
            </text>
          </g>
        ))}
        {bars.map((bar, i) => {
          const x = LEFT + i * band + (band - barWidth) / 2;
          return (
            <g key={i}>
              <rect
                x={x}
                y={y(bar.value)}
                width={barWidth}
                height={Math.max(0, y(0) - y(bar.value))}
                style={{ fill: bar.muted ? `url(#${hatch})` : BAR }}
              />
              {bar.overlay ? (
                <rect x={x} y={y(bar.overlay)} width={barWidth} height={Math.max(0, y(0) - y(bar.overlay))} style={{ fill: "var(--color-alert-icon)" }} />
              ) : null}
              {i % every === 0 && (
                <text x={x + barWidth / 2} y={HEIGHT - BOTTOM + 16} textAnchor="middle" style={text}>
                  {bar.label}
                </text>
              )}
            </g>
          );
        })}
      </svg>
      <ul aria-hidden="true" style={{ display: "flex", gap: Spacing.L, listStyle: "none", margin: 0, padding: 0, color: "var(--color-text-default-on-light)", fontSize: "smaller" }}>
        <LegendItem fill={BAR} label={valueLabel} />
        {overlayLabel && <LegendItem fill="var(--color-alert-icon)" label={overlayLabel} />}
        {hasMuted && mutedLabel && <LegendItem fill={`url(#${hatch})`} label={mutedLabel} hatched />}
      </ul>
    </div>
  );
};

const LegendItem = ({ fill, label, hatched }: { fill: string; label: string; hatched?: boolean }) => (
  <li style={{ display: "flex", alignItems: "center", gap: Spacing.XS }}>
    <span
      style={{
        display: "inline-block",
        width: Spacing.M,
        height: Spacing.M,
        background: hatched ? `repeating-linear-gradient(45deg, ${BAR} 0 2px, ${MUTED} 2px 5px)` : fill,
      }}
    />
    {label}
  </li>
);
