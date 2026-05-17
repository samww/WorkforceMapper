import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import ApplicationDetail from '../../pages/ApplicationDetail';

const APP = {
  id: 4, name: 'AuthService', division: 'Platform Engineering',
  min_support_fte: 4.0, recommended_support_fte: 5.0, current_coverage_fte: 3.0, risk: 'high' as const,
  dependent_products: [
    { id: 1, name: 'DataLicensing', arr_usd: 6200000 },
    { id: 2, name: 'MediaMeasurement', arr_usd: 5100000 },
  ],
  transitive_dependent_apps: [],
  dependent_app_count: 0,
};

describe('ApplicationDetail', () => {
  it('renders without crashing and shows app name', () => {
    render(<ApplicationDetail app={APP} onBack={() => {}} onNavigateToProduct={() => {}} />);
    expect(screen.getByText('AuthService')).toBeInTheDocument();
  });

  it('shows coverage and threshold values', () => {
    render(<ApplicationDetail app={APP} onBack={() => {}} onNavigateToProduct={() => {}} />);
    expect(screen.getByText(/3\.0/)).toBeInTheDocument();
    expect(screen.getByText(/4\.0/)).toBeInTheDocument();
  });

  it('shows dependent products with name and ARR', () => {
    render(<ApplicationDetail app={APP} onBack={() => {}} onNavigateToProduct={() => {}} />);
    expect(screen.getByText('DataLicensing')).toBeInTheDocument();
    expect(screen.getByText('MediaMeasurement')).toBeInTheDocument();
    expect(screen.getByText('$6.2m')).toBeInTheDocument();
    expect(screen.getByText('$5.1m')).toBeInTheDocument();
  });

  it('shows empty state text when dependent_products is empty', () => {
    const appNoProducts = { ...APP, dependent_products: [] };
    render(<ApplicationDetail app={appNoProducts} onBack={() => {}} onNavigateToProduct={() => {}} />);
    expect(screen.getByText('No products depend on this application.')).toBeInTheDocument();
  });

  it('calls onNavigateToProduct with the product id when a product name is clicked', async () => {
    const onNavigateToProduct = vi.fn();
    render(<ApplicationDetail app={APP} onBack={() => {}} onNavigateToProduct={onNavigateToProduct} />);
    await userEvent.click(screen.getByText('DataLicensing'));
    expect(onNavigateToProduct).toHaveBeenCalledWith(1);
  });
});
