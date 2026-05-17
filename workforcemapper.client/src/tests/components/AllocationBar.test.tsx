import { render, container } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import AllocationBar from '../../components/AllocationBar';

describe('AllocationBar', () => {
  it('renders a green bar for normal allocation', () => {
    const { container } = render(<AllocationBar percent={60} mappings={[]} />);
    const bar = container.querySelector('.h-2.rounded.transition-all')!;
    expect(bar.className).toContain('bg-green-500');
    expect(bar.className).not.toContain('bg-red');
  });

  it('does not render a red bar even when given percent > 100', () => {
    const { container } = render(<AllocationBar percent={150} mappings={[]} />);
    const bar = container.querySelector('.h-2.rounded.transition-all')!;
    expect(bar.className).not.toContain('bg-red');
  });

  it('caps bar width at 100% regardless of percent value', () => {
    const { container } = render(<AllocationBar percent={150} mappings={[]} />);
    const bar = container.querySelector<HTMLElement>('.h-2.rounded.transition-all')!;
    expect(bar.style.width).toBe('100%');
  });
});
