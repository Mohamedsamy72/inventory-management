import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import LoginPage from '../src/app/(auth)/login/page';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

/**
 * RTL/Arabic foundations coverage, originally written against the Phase 1 placeholder
 * root page (which rendered a static Arabic statement). Task F2 replaced that page with
 * a server-side `redirect()` to `/dashboard`, which has no renderable content of its own
 * to assert against in a unit test - `/login` is now the first real Arabic content an
 * unauthenticated visitor sees, so these same foundational checks move here.
 */
describe('RTL/Arabic foundations', () => {
  it('renders a heading', () => {
    const { container } = render(<LoginPage />);
    expect(container.querySelector('h1, h2, [data-slot="card-title"]')).not.toBeNull();
  });

  it('renders Arabic user-facing text', () => {
    const { container } = render(<LoginPage />);
    const text = container.textContent ?? '';

    // At least one character in the Arabic Unicode block (docs/31 section 4.2).
    expect(/[؀-ۿ]/.test(text)).toBe(true);
  });

  it('uses no physical direction utilities', () => {
    const { container } = render(<LoginPage />);
    const classNames = Array.from(container.querySelectorAll('*'))
      .map((element) => element.getAttribute('class') ?? '')
      .join(' ');

    // docs/31 section 4.3 — logical properties only.
    expect(/(^|\s)-?(p|m)(l|r)-[\w./[\]-]+(\s|$)/.test(classNames)).toBe(false);
    expect(/(^|\s)text-(left|right)(\s|$)/.test(classNames)).toBe(false);
  });
});
