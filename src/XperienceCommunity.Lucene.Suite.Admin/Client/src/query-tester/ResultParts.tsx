import type { CSSProperties, ReactElement } from "react";
import {
  BorderRadius,
  CellType,
  Colors,
  ColumnContentType,
  Icon,
  Spacing,
  Tag,
  type ComponentCell,
  type StringCell,
  type TableColumn,
} from "@kentico/xperience-admin-components";
import { appliedSynonymRows, firedRuleRows, type AppliedSynonym, type FiredRule } from "./state";

export const column = (name: string, caption: string, maxWidth = 40, minWidth = 12): TableColumn => ({
  name,
  caption,
  visible: true,
  minWidth,
  maxWidth,
  contentType: ColumnContentType.Text,
  sortable: false,
  searchable: false,
});

// Stored fields can hold content HTML: they are only ever passed as plain strings (StringCell, TextWithLabel without valueAsHtml).
export const cell = (value: string, columnName: string): StringCell => ({ type: CellType.String, value, columnName });

/** The npm 31.9.1 ComponentCell renders `component` as a component type with no props: build it from the row's data only, inside a memo. */
export const componentCell = (columnName: string, component: () => ReactElement): ComponentCell => ({ type: CellType.Component, columnName, component });

const wrapStyle: CSSProperties = { display: "flex", flexWrap: "wrap", alignItems: "center", gap: Spacing.S };

const chipStyle: CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  gap: Spacing.XS,
  maxWidth: "100%",
  padding: "4px 10px",
  border: `${Spacing.Micro} solid var(--color-border-default)`,
  borderRadius: BorderRadius.RadiusM,
  color: "var(--color-product-hover)",
  textDecoration: "none",
};

/** One chip per fired rule, linking to its edit page; the icon is decorative, so the chip's name is its text. */
export const RuleChips = ({ rules }: { rules: readonly FiredRule[] }) => (
  <div style={wrapStyle}>
    {firedRuleRows(rules, window.location.pathname).map((rule) =>
      rule.href ? (
        <a key={rule.ruleId} href={rule.href} style={chipStyle}>
          <span aria-hidden="true" style={{ display: "inline-flex" }}>
            <Icon name={rule.icon} />
          </span>
          <span>
            <strong>{rule.label}</strong>
            {rule.summary && ` · ${rule.summary}`}
          </span>
        </a>
      ) : (
        <span key={rule.ruleId} style={{ ...chipStyle, color: "var(--color-text-low-emphasis)" }}>{`${rule.label} (deleted)`}</span>
      ),
    )}
  </div>
);

/** Applied synonyms as Tags that open the synonym's edit page; a synonym deleted since is plain text. */
export const SynonymTags = ({ synonyms }: { synonyms: readonly AppliedSynonym[] }) => (
  <div style={wrapStyle}>
    {appliedSynonymRows(synonyms, window.location.pathname).map(({ key, label, href }) =>
      href ? (
        <Tag key={key} label={label} onClick={() => window.location.assign(href)} />
      ) : (
        <span key={key}>{`${label} (deleted)`}</span>
      ),
    )}
  </div>
);

/** Result title on one line, after a "Pinned #n" Tag when a rule pinned the hit. */
export const TitleCell = ({ title, pinnedAt, pinnedBy }: { title: string; pinnedAt: number | null; pinnedBy: string | null }) => (
  <span style={{ display: "flex", alignItems: "center", gap: Spacing.S, minWidth: 0 }}>
    {pinnedAt !== null && (
      <span style={{ flexShrink: 0 }}>
        <Tag label={`Pinned #${pinnedAt}`} background={{ color: Colors.BackgroundSelected }} tooltipText={pinnedBy ? `Pinned by ${pinnedBy}` : undefined} />
      </span>
    )}
    <span style={{ minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{title}</span>
  </span>
);
