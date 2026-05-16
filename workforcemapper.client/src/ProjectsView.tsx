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

export default function ProjectsView({ month, division }: Props) {
  const [projects, setProjects] = useState<ProjectAllocation[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    fetch(`/api/projects?month=${month}`)
      .then(r => r.json())
      .then((data: ProjectAllocation[]) => {
        data.sort((a, b) => a.name.localeCompare(b.name));
        setProjects(data);
        setLoading(false);
      });
  }, [month]);

  const visible = division === 'All'
    ? projects
    : projects.filter(p => p.divisions.includes(division));

  const barMax = visible.length > 0
    ? Math.max(...visible.map(p => p.allocation_fte)) || 1
    : 1;

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
          <tr className="text-left text-gray-400 border-b border-gray-700">
            <th className="pb-2 pr-4">Name</th>
            <th className="pb-2 pr-4">Divisions</th>
            <th className="pb-2">Allocation</th>
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
