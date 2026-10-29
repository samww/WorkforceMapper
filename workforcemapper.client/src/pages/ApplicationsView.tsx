import { useEffect, useState } from 'react';

export interface ProductRef {
  id: number;
  name: string;
  arr_usd: number;
  risk?: 'high' | 'medium' | 'low';
}

export interface ApplicationRisk {
  id: number;
  name: string;
  division: string;
  min_support_fte: number;
  recommended_support_fte: number;
  current_coverage_fte: number;
  risk: 'high' | 'medium' | 'low';
  dependent_products: ProductRef[];
  transitive_dependent_apps: { id: number; name: string; risk: string }[];
  dependent_app_count: number;
}

interface Props {
  month: string;
  division: string;
  onSelectApp: (app: ApplicationRisk) => void;
}

const BADGE: Record<string, string> = {
  high: 'bg-red-600 text-white',
  medium: 'bg-amber-500 text-white',
  low: 'bg-green-600 text-white',
};

export function CoverageBar({ current, min, recommended, barMax }: { current: number; min: number; recommended: number; barMax: number }) {
  const coveragePct = (current / barMax) * 100;
  const minPct = (min / barMax) * 100;
  const recPct = (recommended / barMax) * 100;

  // Nudge min/rec labels apart if closer than 15% of bar width
  const gap = recPct - minPct;
  const nudge = gap < 15 ? (15 - gap) / 2 : 0;
  const minLabelPct = minPct - nudge;
  const recLabelPct = recPct + nudge;

  return (
    <div className="flex items-center gap-1 text-xs">
      <span className="text-gray-500 shrink-0">0</span>
      <div className="relative flex-1" style={{ minWidth: '8rem' }}>
        <div className="relative h-4">
          <span className="absolute -translate-x-1/2 text-amber-400" style={{ left: `${minLabelPct}%` }}>{min.toFixed(1)}</span>
          <span className="absolute -translate-x-1/2 text-green-400" style={{ left: `${recLabelPct}%` }}>{recommended.toFixed(1)}</span>
        </div>
        <div className="relative h-2.5 bg-gray-700 rounded">
          <div data-testid="coverage-fill" className="h-full bg-blue-500 rounded" style={{ width: `${coveragePct}%` }} />
          <div data-testid="coverage-min-marker" className="absolute top-0 h-full w-0.5 bg-amber-400" style={{ left: `${minPct}%` }} />
          <div data-testid="coverage-rec-marker" className="absolute top-0 h-full w-0.5 bg-green-400" style={{ left: `${recPct}%` }} />
        </div>
        <div className="relative h-4">
          <span className="absolute -translate-x-1/2 text-blue-400" style={{ left: `${coveragePct}%` }}>{current.toFixed(1)}</span>
        </div>
      </div>
      <span className="text-gray-500 shrink-0">{barMax.toFixed(1)}</span>
    </div>
  );
}

type AppSortCol = 'name' | 'division' | 'risk' | 'coverage';

const RISK_ORDER: Record<string, number> = { high: 2, medium: 1, low: 0 };

const COL_FIRST_DIR: Record<AppSortCol, 'asc' | 'desc'> = {
  name: 'asc',
  division: 'asc',
  risk: 'desc',
  coverage: 'asc',
};

function sortApps(apps: ApplicationRisk[], col: AppSortCol, dir: 'asc' | 'desc'): ApplicationRisk[] {
  return [...apps].sort((a, b) => {
    let cmp = 0;
    if (col === 'name') cmp = a.name.localeCompare(b.name);
    else if (col === 'division') cmp = a.division.localeCompare(b.division);
    else if (col === 'risk') cmp = RISK_ORDER[a.risk] - RISK_ORDER[b.risk];
    else if (col === 'coverage') cmp = a.current_coverage_fte - b.current_coverage_fte;
    const primary = dir === 'asc' ? cmp : -cmp;
    if (primary !== 0) return primary;
    return a.name.localeCompare(b.name);
  });
}

export default function ApplicationsView({ month, division, onSelectApp }: Props) {
  const [apps, setApps] = useState<ApplicationRisk[]>([]);
  const [loadedMonth, setLoadedMonth] = useState<string | null>(null);
  const loading = loadedMonth !== month;
  const [sortCol, setSortCol] = useState<AppSortCol>('risk');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');

  useEffect(() => {
    let cancelled = false;
    fetch(`/api/applications?month=${month}`)
      .then(r => r.json())
      .then((data: ApplicationRisk[]) => {
        if (cancelled) return;
        setApps(data);
        setLoadedMonth(month);
      });
    return () => { cancelled = true; };
  }, [month]);

  function handleSort(col: AppSortCol) {
    if (col === sortCol) {
      setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    } else {
      setSortCol(col);
      setSortDir(COL_FIRST_DIR[col]);
    }
  }

  const filtered = division === 'All' ? apps : apps.filter(a => a.division === division);
  const visible = sortApps(filtered, sortCol, sortDir);
  const barMax = visible.length > 0
    ? Math.max(...visible.flatMap(a => [a.current_coverage_fte, a.min_support_fte, a.recommended_support_fte]))
    : 1;

  function colHeaderProps(col: AppSortCol) {
    const active = col === sortCol;
    const ariaSortMap = { asc: 'ascending' as const, desc: 'descending' as const };
    return {
      'aria-sort': active ? ariaSortMap[sortDir] : ('none' as const),
      className: `pb-2 px-2 sm:px-0 sm:pr-4 ${col === 'division' ? 'hidden sm:table-cell' : ''}`,
    };
  }

  function colBtnClass(col: AppSortCol) {
    return col === sortCol
      ? 'text-gray-100 cursor-pointer'
      : 'text-gray-400 hover:text-gray-200 cursor-pointer';
  }

  function arrow(col: AppSortCol) {
    if (col !== sortCol) return null;
    return sortDir === 'asc' ? ' ▲' : ' ▼';
  }

  if (loading) return (
    <div className="flex justify-center items-center h-40">
      <div role="status" className="w-8 h-8 rounded-full border-2 border-gray-600 border-t-blue-400 animate-spin" />
    </div>
  );

  return (
    <div className="p-4">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-semibold text-gray-100">Applications</h2>
      </div>
      <table className="w-full text-sm text-gray-200">
        <thead>
          <tr className="text-left border-b border-gray-700">
            <th {...colHeaderProps('name')}><button className={colBtnClass('name')} onClick={() => handleSort('name')}>Name{arrow('name')}</button></th>
            <th {...colHeaderProps('division')}><button className={colBtnClass('division')} onClick={() => handleSort('division')}>Division{arrow('division')}</button></th>
            <th {...colHeaderProps('risk')}><button className={colBtnClass('risk')} onClick={() => handleSort('risk')}>Risk{arrow('risk')}</button></th>
            <th aria-sort={sortCol === 'coverage' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'} className="pb-2 px-2 sm:px-0"><button className={colBtnClass('coverage')} onClick={() => handleSort('coverage')}>Coverage{arrow('coverage')}</button></th>
          </tr>
        </thead>
        <tbody>
          {visible.map(app => (
            <tr
              key={app.id}
              className="border-b border-gray-800 hover:bg-gray-800 cursor-pointer"
              onClick={() => onSelectApp(app)}
            >
              <td className="py-2 px-2 sm:px-0 sm:pr-4">{app.name}</td>
              <td className="hidden py-2 px-2 text-gray-400 sm:table-cell sm:px-0 sm:pr-4">{app.division}</td>
              <td className="py-2 px-2 sm:px-0 sm:pr-4">
                <span className={`px-2 py-0.5 rounded text-xs font-medium ${BADGE[app.risk]}`}>
                  {app.risk}
                </span>
              </td>
              <td className="py-2 px-2 sm:px-0">
                <CoverageBar current={app.current_coverage_fte} min={app.min_support_fte} recommended={app.recommended_support_fte} barMax={barMax} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
