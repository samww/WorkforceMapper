import { useEffect, useState } from 'react';
import type { Resource, Activity, ResourceMapping } from './types';
import AllocationBar from './AllocationBar';

const DEFAULT_MONTH = '2026-05';
const DIVISIONS = ['All', 'Platform Engineering', 'Analytics', 'Data Collection', 'Client Services'];
const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const MONTH_OPTIONS = Array.from({ length: 12 }, (_, i) => ({
  value: `2026-${String(i + 1).padStart(2, '0')}`,
  label: `${MONTH_LABELS[i]} 2026`,
}));

interface MappingViewProps {
  month: string;
  division: string;
  onOpenChat?: () => void;
}

export default function MappingView({ month, division, onOpenChat }: MappingViewProps) {
  const [resources, setResources] = useState<Resource[]>([]);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [loading, setLoading] = useState(true);
  const [unallocatedOnly, setUnallocatedOnly] = useState(false);
  const [zeroMonths, setZeroMonths] = useState<Set<string>>(new Set());

  function refreshZeroMonths() {
    Promise.all(
      MONTH_OPTIONS.map((o) =>
        fetch(`/api/resources?month=${o.value}`).then((r) => r.json() as Promise<Resource[]>)
      )
    ).then((results) => {
      const zeros = new Set(
        MONTH_OPTIONS.filter((_o, i) =>
          results[i].every((r) => r.mappings.reduce((s, m) => s + m.percent, 0) === 0)
        ).map((o) => o.value)
      );
      setZeroMonths(zeros);
    });
  }

  useEffect(() => { refreshZeroMonths(); }, []);

  useEffect(() => {
    setLoading(true);
    Promise.all([
      fetch(`/api/resources?month=${month}`).then(r => r.json()),
      fetch('/api/activities').then(r => r.json()),
    ]).then(([resourceData, activityData]: [Resource[], Activity[]]) => {
      setResources(resourceData);
      const projects = activityData.filter(a => a.activity_type === 'Project').sort((a, b) => a.id - b.id);
      const apps = activityData.filter(a => a.activity_type !== 'Project').sort((a, b) => a.name.localeCompare(b.name));
      setActivities([...projects, ...apps]);
      setLoading(false);
    });
  }, [month]);

  const visible = resources.filter((r) => {
    if (division !== 'All' && r.division !== division) return false;
    if (unallocatedOnly) {
      const total = r.mappings.reduce((sum, m) => sum + m.percent, 0);
      if (total >= 100) return false;
    }
    return true;
  }).sort((a, b) => a.name.localeCompare(b.name));

  function refreshResources() {
    fetch(`/api/resources?month=${month}`)
      .then((r) => r.json())
      .then(setResources);
    refreshZeroMonths();
  }

  if (loading) return (
    <div className="flex justify-center items-center h-40">
      <div role="status" className="w-8 h-8 rounded-full border-2 border-gray-600 border-t-blue-400 animate-spin" />
    </div>
  );

  return (
    <div className="min-h-screen bg-gray-900 text-gray-100 p-6">
      <div className="max-w-7xl mx-auto">
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-bold">Workforce Mapper</h1>
          {onOpenChat && (
            <button
              onClick={onOpenChat}
              className="bg-blue-600 hover:bg-blue-500 text-white rounded-lg px-4 py-2 text-sm font-medium"
            >
              Ask AI
            </button>
          )}
        </div>

        <div className="flex flex-wrap gap-4 mb-6 items-center">
          <label className="flex items-center gap-2 text-sm cursor-pointer">
            <input
              type="checkbox"
              checked={unallocatedOnly}
              onChange={(e) => setUnallocatedOnly(e.target.checked)}
              aria-label="Unallocated only"
            />
            Show unallocated only
          </label>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {visible.map((resource) => (
            <ResourceCard
              key={resource.id}
              resource={resource}
              activities={activities}
              month={month}
              onMappingChange={refreshResources}
            />
          ))}
        </div>

        {visible.length === 0 && (
          <p className="text-gray-400 text-center mt-12">No resources match the current filters.</p>
        )}
      </div>
    </div>
  );
}

interface CardProps {
  resource: Resource;
  activities: Activity[];
  month: string;
  onMappingChange: () => void;
}

function allocationColor(pct: number) {
  if (pct >= 90) return 'text-amber-400';
  return 'text-green-400';
}

function monthLabel(month: string) {
  return MONTH_OPTIONS.find((o) => o.value === month)?.label ?? month;
}

async function parseApiError(res: Response, resourceName: string, month: string): Promise<string> {
  if (res.status === 400) {
    try {
      const body = await res.json();
      if (body.error === 'would_exceed_cap')
        return `Cannot allocate more than 100% to ${resourceName} in ${monthLabel(month)}`;
      if (body.error === 'resource_inactive_in_month')
        return `${resourceName} is not active in ${monthLabel(month)}`;
    } catch { /* ignore */ }
  }
  return 'Save failed';
}

function ResourceCard({ resource, activities, month, onMappingChange }: CardProps) {
  const [adding, setAdding] = useState(false);
  const [editingActivityId, setEditingActivityId] = useState<number | null>(null);
  const [newActivityId, setNewActivityId] = useState('');
  const [newPercent, setNewPercent] = useState('');
  const [editActivityId, setEditActivityId] = useState('');
  const [editPercent, setEditPercent] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const totalPct = resource.mappings.reduce((sum, m) => sum + m.percent, 0);

  function startEdit(m: ResourceMapping) {
    setEditingActivityId(m.activity_id);
    setEditActivityId(String(m.activity_id));
    setEditPercent(String(m.percent));
  }

  function cancelEdit() {
    setEditingActivityId(null);
  }

  async function handleSaveEdit(originalActivityId: number) {
    if (!editActivityId || !editPercent) return;
    const newActId = parseInt(editActivityId);
    const newPct = parseInt(editPercent, 10);
    if (newPct < 1 || newPct > 100) return;
    setError(null);

    if (!resource.active) {
      setError(`${resource.name} is not active in ${monthLabel(month)}`);
      return;
    }

    setSaving(true);
    if (newActId !== originalActivityId) {
      const del = await fetch(`/api/mappings?resource_id=${resource.id}&activity_id=${originalActivityId}&month=${month}`, { method: 'DELETE' });
      if (!del.ok) { setSaving(false); setError(await parseApiError(del, resource.name, month)); return; }
    }
    const res = await fetch('/api/mappings', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ resource_id: resource.id, activity_id: newActId, month, percent: newPct }),
    });
    setSaving(false);
    if (!res.ok) { setError(await parseApiError(res, resource.name, month)); return; }
    setEditingActivityId(null);
    onMappingChange();
  }

  async function handleAdd() {
    if (!newActivityId || !newPercent) return;
    const pct = parseInt(newPercent, 10);
    if (pct < 1 || pct > 100) return;
    setError(null);

    if (!resource.active) {
      setError(`${resource.name} is not active in ${monthLabel(month)}`);
      return;
    }

    setSaving(true);
    const res = await fetch('/api/mappings', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        resource_id: resource.id,
        activity_id: parseInt(newActivityId),
        month,
        percent: pct,
      }),
    });
    setSaving(false);
    if (!res.ok) { setError(await parseApiError(res, resource.name, month)); return; }
    setAdding(false);
    setNewActivityId('');
    setNewPercent('');
    onMappingChange();
  }

  async function handleDelete(activityId: number) {
    setError(null);
    if (!resource.active) {
      setError(`${resource.name} is not active in ${monthLabel(month)}`);
      return;
    }
    setSaving(true);
    const res = await fetch(`/api/mappings?resource_id=${resource.id}&activity_id=${activityId}&month=${month}`, { method: 'DELETE' });
    setSaving(false);
    if (!res.ok) { setError(await parseApiError(res, resource.name, month)); return; }
    onMappingChange();
  }

  return (
    <div data-card className="bg-gray-800 rounded-lg p-4 flex flex-col gap-2 border border-gray-700">
      <div className="flex justify-between items-start">
        <div>
          <p className="font-semibold">{resource.name}</p>
          <p className="text-xs text-gray-400">{resource.role_type} · {resource.seniority}</p>
          <p className="text-xs text-gray-400">{resource.resource_type} · {resource.country}</p>
          <p className="text-xs text-gray-500 mt-0.5">{resource.division}</p>
        </div>
        <div className="flex items-center gap-2">
          {saving && <div role="status" className="w-4 h-4 rounded-full border-2 border-gray-600 border-t-blue-400 animate-spin" />}
          <span className={`text-sm font-bold ${allocationColor(totalPct)}`}>{totalPct}%</span>
        </div>
      </div>

      <AllocationBar percent={totalPct} mappings={resource.mappings} />
      {error && <p className="text-xs text-red-400 mt-1">{error}</p>}

      <div className="mt-2 space-y-1">
        {resource.mappings.map((m) =>
          editingActivityId === m.activity_id ? (
            <div key={m.activity_id} className="flex flex-col gap-1 text-xs">
              <select
                value={editActivityId}
                onChange={(e) => setEditActivityId(e.target.value)}
                className="bg-gray-700 border border-gray-600 rounded px-2 py-1 w-full"
              >
                {activities.map((a) => (
                  <option key={a.id} value={a.id}>{a.name}</option>
                ))}
              </select>
              <div className="flex gap-2 items-center">
                <input
                  type="text"
                  inputMode="numeric"
                  value={editPercent}
                  onChange={(e) => setEditPercent(e.target.value.replace(/\D/g, '').slice(0, 3))}
                  className="bg-gray-700 border border-gray-600 rounded px-2 py-1 w-20"
                />
                <button
                  onClick={() => handleSaveEdit(m.activity_id)}
                  className="bg-blue-600 hover:bg-blue-500 rounded px-2 py-1"
                  aria-label="Save"
                >
                  Save
                </button>
                <button
                  onClick={cancelEdit}
                  className="text-gray-400 hover:text-gray-200"
                >
                  Cancel
                </button>
              </div>
            </div>
          ) : (
            <div key={m.activity_id} className="flex items-center justify-between text-xs">
              <span className="text-gray-300 truncate flex-1 mr-2">{m.activity_name}</span>
              <span className="text-gray-400 mr-2">{m.percent}%</span>
              <button
                onClick={() => startEdit(m)}
                className="text-blue-400 hover:text-blue-300 mr-1"
                aria-label={`Edit ${m.activity_name}`}
              >
                ✎
              </button>
              <button
                onClick={() => handleDelete(m.activity_id)}
                className="text-red-400 hover:text-red-300"
                aria-label={`Remove ${m.activity_name}`}
              >
                ✕
              </button>
            </div>
          )
        )}
      </div>

      {adding ? (
        <div className="mt-2 flex flex-col gap-2">
          <select
            value={newActivityId}
            onChange={(e) => setNewActivityId(e.target.value)}
            className="bg-gray-700 border border-gray-600 rounded px-2 py-1 text-xs w-full"
          >
            <option value="">Select activity…</option>
            {activities.map((a) => (
              <option key={a.id} value={a.id}>{a.name}</option>
            ))}
          </select>
          <div className="flex gap-2">
            <input
              type="text"
              inputMode="numeric"
              value={newPercent}
              onChange={(e) => setNewPercent(e.target.value.replace(/\D/g, '').slice(0, 3))}
              placeholder="%"
              className="bg-gray-700 border border-gray-600 rounded px-2 py-1 text-xs w-20"
            />
            <button
              onClick={handleAdd}
              className="bg-blue-600 hover:bg-blue-500 rounded px-3 py-1 text-xs"
            >
              Add
            </button>
            <button
              onClick={() => setAdding(false)}
              className="text-gray-400 hover:text-gray-200 text-xs"
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <button
          onClick={() => setAdding(true)}
          className="mt-1 text-xs text-blue-400 hover:text-blue-300 text-left"
        >
          + Add mapping
        </button>
      )}
    </div>
  );
}
