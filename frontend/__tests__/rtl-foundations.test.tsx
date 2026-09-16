import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import HomePage from '../src/app/page';

/**
 * Phase 1 frontend coverage.
 *
 * The component library, formatters and status-badge mapping are Phase F1. What is
 * assertable now is that the shell renders and that its content honours the Arabic-only
 * rule — so a regression that slips an English string in gets caught on day one rather
 * than at the end of F6.
 */
describe('Phase 1 shell', () => {
  it('renders the root page', () => {
    const { container } = render(<HomePage />);
    expect(container.querySelector('h1')).not.toBeNull();
  });

  it('renders Arabic user-facing text', () => {
    const { container } = render(<HomePage />);
    const text = container.textContent ?? '';

    // At least one character in the Arabic Unicode block (docs/31 section 4.2).
    expect(/[؀-ۿ]/.test(text)).toBe(true);
  });

  it('uses no physical direction utilities', () => {
    const { container } = render(<HomePage />);
    const classNames = Array.from(container.querySelectorAll('*'))
      .map((element) => element.getAttribute('class') ?? '')
      .join(' ');

    // docs/31 section 4.3 — logical properties only.
    expect(/(^|\s)-?(p|m)(l|r)-/.test(classNames)).toBe(false);
    expect(/(^|\s)text-(left|right)(\s|$)/.test(classNames)).toBe(false);
  });
});
