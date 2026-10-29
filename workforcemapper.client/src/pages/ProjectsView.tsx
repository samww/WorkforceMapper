import { useEffect, useState } from 'react';

interface ProjectAllocation {
  id: number;
  name: string;
  divisions: string[];
  allocation_fte: number;
}

interface Props {
  month: string;
  division: string;
}

function AllocationBar({ allocation, barMax }: { allocation: number; barMax: number }) {
  const fillPct = barMax > 0 ? (allocation / barMax) * 100 : 0;
  return (
    <div className="flex items-center gap-1 text-xs">
      <span className="text-gray-500 shrink-0">0</span>
      <div className="relative flex-1" style={{ minWidth: '8rem' }}>
        {/* h-4 spacer above mirrors the h-4 label below so items-center aligns end-labels with the track */}
        <div className="h-4" />
        <div className="relative h-2.5 bg-gray-700 rounded">
          <div
            data-testid="allocation-fill"
            className="h-full bg-blue-500 rounded"
            style={{ width: `${fillPct}%` }}
          />
        </div>
        <div className="relative h-4">
          <span
            className="absolute -translate-x-1/2 text-blue-400"
            style={{ left: `${fillPct}%` }}
          >
            {allocation.toFixed(1)}
          </span>
        </div>
      </div>
      <span className="text-gray-500 shrink-0">{barMax.toFixed(1)}</span>
    </div>
  );
}

type ProjectSortCol = 'name' | 'divisions' | 'allocation';

const PROJECT_COL_FIRST_DIR: Record<ProjectSortCol, 'asc' | 'desc'> = {
  name: 'asc',
  divisions: 'asc',
  allocation: 'desc',
};

function sortProjects(projects: ProjectAllocation[], col: ProjectSortCol, dir: 'asc' | 'desc'): ProjectAllocation[] {
  return [...projects].sort((a, b) => {
    let cmp = 0;
    if (col === 'name') cmp = a.name.localeCompare(b.name);
    else if (col === 'divisions') cmp = [...a.divisions].sort()[0].localeCompare([...b.divisions].sort()[0]);
    else if (col === 'allocation') cmp = a.allocation_fte - b.allocation_fte;
    const primary = dir === 'asc' ? cmp : -cmp;
    if (primary !== 0) return primary;
    return a.name.localeCompare(b.name);
  });
}

export default function ProjectsView({ month, division }: Props) {
  const [projects, setProjects] = useState<ProjectAllocation[]>([]);
  const [loadedMonth, setLoadedMonth] = useState<string | null>(null);
  const loading = loadedMonth !== month;
  const [sortCol, setSortCol] = useState<ProjectSortCol>('name');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('asc');

  useEffect(() => {
    fetch(`/api/projects?month=${month}`)
      .then(r => r.json())
      .then((data: ProjectAllocation[]) => {
        setProjects(data);
        setLoadedMonth(month);
      });
  }, [month]);

  function handleSort(col: ProjectSortCol) {
    if (col === sortCol) {
      setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    } else {
      setSortCol(col);
      setSortDir(PROJECT_COL_FIRST_DIR[col]);
    }
  }

  const filtered = division === 'All'
    ? projects
    : projects.filter(p => p.divisions.includes(division));

  const visible = sortProjects(filtered, sortCol, sortDir);

  const barMax = visible.length > 0
    ? Math.max(...visible.map(p => p.allocation_fte)) || 1
    : 1;

  function colBtnClass(col: ProjectSortCol) {
    return col === sortCol
      ? 'text-gray-100 cursor-pointer'
      : 'text-gray-400 hover:text-gray-200 cursor-pointer';
  }

  function arrow(col: ProjectSortCol) {
    if (col !== sortCol) return null;
    return sortDir === 'asc' ? ' ▲' : ' ▼';
  }

  function ariaSortAttr(col: ProjectSortCol): 'ascending' | 'descending' | 'none' {
    if (col !== sortCol) return 'none';
    return sortDir === 'asc' ? 'ascending' : 'descending';
  }

  if (loading) return (
    <div className="flex justify-center items-center h-40">
      <div role="status" className="w-8 h-8 rounded-full border-2 border-gray-600 border-t-blue-400 animate-spin" />
    </div>
  );

  return (
    <div className="p-4">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-semibold text-gray-100">Projects</h2>
      </div>
      <table className="w-full text-sm text-gray-200">
        <thead>
          <tr className="text-left border-b border-gray-700">
            <th aria-sort={ariaSortAttr('name')} className="pb-2 pr-4"><button className={colBtnClass('name')} onClick={() => handleSort('name')}>Name{arrow('name')}</button></th>
            <th aria-sort={ariaSortAttr('divisions')} className="pb-2 pr-4"><button className={colBtnClass('divisions')} onClick={() => handleSort('divisions')}>Divisions{arrow('divisions')}</button></th>
            <th aria-sort={ariaSortAttr('allocation')} className="pb-2"><button className={colBtnClass('allocation')} onClick={() => handleSort('allocation')}>Allocation{arrow('allocation')}</button></th>
          </tr>
        </thead>
        <tbody>
          {visible.map(project => (
            <tr key={project.id} className="border-b border-gray-800">
              <td className="py-2 pr-4">{project.name}</td>
              <td className="py-2 pr-4 text-gray-400">{project.divisions.join(', ')}</td>
              <td className="py-2">
                <AllocationBar allocation={project.allocation_fte} barMax={barMax} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
