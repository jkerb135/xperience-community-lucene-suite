import type { CSSProperties, ReactNode } from "react";
import { Headline, HeadlineSize, Spacing } from "@kentico/xperience-admin-components";
import type { Tone } from "./report.ts";

// Runtime tokens verified in the 31.9.1 admin (getComputedStyle); the kit repo has newer ones that resolve to nothing here.
// npm 31.9.1 Spacing values are strings ("1px"), so they are used as-is, never with a unit appended.
export const TONE_COLOR: Record<Tone, string> = {
  good: "var(--color-success-text)",
  bad: "var(--color-alert-text)",
  neutral: "var(--color-text-low-emphasis)",
};
const TONE_WORD: Record<Tone, string> = { good: "better", bad: "worse", neutral: "" };
export const ACCENT = "linear-gradient(135deg, var(--color-product), var(--color-product-hover))";
export const BORDER = `${Spacing.Micro} solid var(--color-paper-border)`;

const eyebrowStyle: CSSProperties = {
  display: "block",
  textTransform: "uppercase",
  letterSpacing: "0.12em",
  fontSize: "smaller",
  fontWeight: 600,
  color: "var(--color-text-low-emphasis)",
};

export const visuallyHidden: CSSProperties = {
  position: "absolute",
  width: 1,
  height: 1,
  margin: -1,
  padding: 0,
  overflow: "hidden",
  clip: "rect(0 0 0 0)",
  whiteSpace: "nowrap",
  border: 0,
};

export const Eyebrow = ({ children, color }: { children: ReactNode; color?: string }) => (
  <span style={color ? { ...eyebrowStyle, color } : eyebrowStyle}>{children}</span>
);

/** A section heading with an eyebrow line above it. */
export const SectionHeading = ({ eyebrow, title, level = 2, size = HeadlineSize.M }: { eyebrow: string; title: string; level?: 2 | 3; size?: HeadlineSize }) => (
  <div>
    <Eyebrow>{eyebrow}</Eyebrow>
    <div role="heading" aria-level={level}>
      <Headline size={size}>{title}</Headline>
    </div>
  </div>
);

/** Headline for an npm Card: eyebrow line plus the level-2 title. */
export const cardHeadline = (eyebrow: string, title: string) => (
  <span>
    <Eyebrow>{eyebrow}</Eyebrow>
    <span role="heading" aria-level={2}>
      {title}
    </span>
  </span>
);

/** Signed change text plus a hidden better/worse, so the meaning is never colour-only. */
export const DeltaText = ({ text, tone, onAccent }: { text: string; tone: Tone; onAccent?: boolean }) => (
  <span style={{ color: onAccent ? "inherit" : TONE_COLOR[tone], fontWeight: tone === "neutral" ? 400 : 600 }}>
    {text}
    {TONE_WORD[tone] && <span style={visuallyHidden}>{` (${TONE_WORD[tone]})`}</span>}
  </span>
);

/** White dashboard panel; the npm Paper adds no padding of its own. */
export const panelStyle: CSSProperties = { padding: Spacing.XL };

// Module-level so the callback ref is stable: it focuses a panel's content once, when it mounts.
export const focusOnMount = (element: HTMLElement | null) => element?.focus();
