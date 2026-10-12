import type { CSSProperties } from "react";

/**
 * Root style of every custom Suite page template: the same right gutter native Kentico 31.9.1 pages keep between
 * their content and the scrollbar (listings and edit forms measure 104 px at every width; a 13 × 8 px column).
 */
export const pageStyle: CSSProperties = { paddingRight: "104px" };
