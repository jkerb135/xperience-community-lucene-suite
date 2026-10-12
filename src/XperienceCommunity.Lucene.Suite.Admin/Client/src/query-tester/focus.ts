interface FocusDocument {
  readonly activeElement: Element | null;
  readonly body: Element | null;
}

interface Focusable {
  focus(): void;
}

const isActive = (doc: FocusDocument, element: Focusable) => (doc.activeElement as unknown) === element;

/** True for elements inside a Kentico SidePanel (31.9.1 renders no dialog role, only this class). */
export const inSidePanel = (element: Element) => element.closest?.('[class*="side-panel___"]') != null;

const budgetMs = 1000;

/**
 * Returns focus to a panel's opener after the panel closes. The SidePanel may move focus itself a few frames later,
 * so this retries each frame until the target holds focus, but only while focus is lost (null, body, detached) or still
 * in a panel: once anything else has focus the user has moved on and is left alone.
 */
export const returnFocus = (
  target: () => Focusable | null | undefined,
  schedule: (callback: () => void) => unknown = requestAnimationFrame,
  now: () => number = () => performance.now(),
  doc: FocusDocument = document,
  inPanel: (element: Element) => boolean = inSidePanel,
) => {
  const start = now();
  const lost = () => {
    const active = doc.activeElement;
    return active === null || active === doc.body || !(active as Element & { isConnected?: boolean }).isConnected || inPanel(active);
  };
  const tick = () => {
    try {
      const element = target();
      if (!element) {
        return;
      }
      if (!isActive(doc, element)) {
        if (!lost()) {
          return;
        }
        element.focus();
      }
      // Keep watching until the budget ends: the panel can take focus back after it held.
      if (now() - start < budgetMs) {
        schedule(tick);
      }
    } catch {
      // Focus is best effort; never break the page.
    }
  };
  schedule(tick);
};

/** The opener to return to: the chip that opened the panel while it still exists, else the fallback (e.g. the Filter button). */
export const openerOrFallback = <T extends { readonly isConnected?: boolean }>(opener: T | null | undefined, fallback: T | null | undefined) =>
  opener && opener.isConnected !== false ? opener : fallback;
