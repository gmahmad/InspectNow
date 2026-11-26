import { useState } from 'react';
import { Badge, Empty, Feedback, Loading, WorkPage, useAction } from './components.jsx';
import { useWorkspaceData } from './team.jsx';
import { routeLink } from './routes.js';

const loadActions = workspace => workspace.correctiveActions();
const loadInspectors = async workspace => (await workspace.members()).filter(member => member.role === 'Inspector' && member.canUseInspections !== false);
const labels = { Open: 'Open', ReadyForReview: 'Ready for review', Closed: 'Closed' };

export function CorrectiveActionForm({ workspace, record, question, onCreated }) {
  const { data, error } = useWorkspaceData(workspace, loadInspectors);
  const { busy, error: actionError, run } = useAction();
  if (!data) return error ? <Feedback message={error} /> : <Loading />;
  if (!data.length) return <Empty title="No inspectors available">Invite an inspector from the Team page before assigning this action.</Empty>;
  return <form className="form-stack" onSubmit={event => {
    event.preventDefault();
    const values = Object.fromEntries(new FormData(event.currentTarget));
    run(async () => {
      await workspace.createCorrectiveAction({ ...values, inspectionId: record.id, questionId: question.id });
      onCreated();
    });
  }}>
    <Feedback message={actionError} />
    <p><strong>{question.text}</strong></p>
    <label>What needs to be fixed?<textarea name="instructions" rows={3} maxLength={2000} required /></label>
    <label>Inspector<select name="assigneeId" required>
      <option value="">Choose an inspector</option>{data.map(member => <option key={member.id} value={member.id}>{member.email}</option>)}
    </select></label>
    <label>Due date<input type="date" name="dueDate" required /></label>
    <div className="dialog-actions"><button className="button primary" disabled={busy}>Assign action</button></div>
  </form>;
}

function ActionDetails({ item, workspace, canManage, onUpdate, onReload, onOpenInspection }) {
  const { busy, error, run } = useAction();
  const [note, setNote] = useState('');
  const resolve = !canManage && item.status === 'Open';
  const review = canManage && item.status === 'ReadyForReview';
  const update = approve => run(async () => {
    if (resolve) await workspace.resolveCorrectiveAction(item.id, { expectedVersion: item.version, resolution: note.trim() });
    else await workspace.reviewCorrectiveAction(item.id, { expectedVersion: item.version, approve, note: note.trim() || null });
    onUpdate();
  });
  return <WorkPage title={item.siteName} description={item.finding} back="#corrective-actions" backLabel="Back to corrective actions" layout="reading">
    <div className="panel work-panel form-stack">
      <div className="detail-summary"><Badge status={item.status} label={labels[item.status]} /><span>Due {item.dueDate}</span></div>
      {canManage && <div><button className="button secondary small" onClick={() => onOpenInspection(item.inspectionId)}>View source inspection</button></div>}
      <Feedback message={error} />
      {item.findingNote && <div><strong>Inspection note</strong><p>{item.findingNote}</p></div>}
      <div><strong>Instructions</strong><p>{item.instructions}</p></div>
      {item.resolution && <div><strong>Resolution</strong><p>{item.resolution}</p></div>}
      {item.reviewNote && <div><strong>Review note</strong><p>{item.reviewNote}</p></div>}
      {(resolve || review) && <>
        <label>{resolve ? 'What did you fix?' : 'Review note'}<textarea value={note} onChange={event => setNote(event.target.value)} rows={3} maxLength={2000} /></label>
        <div className="dialog-actions">
          {error && <button className="button secondary" onClick={onReload}>Reload actions</button>}
          {review && <button className="button secondary" disabled={busy || !note.trim()} onClick={() => update(false)}>Return for changes</button>}
          <button className="button primary" disabled={busy || (resolve && !note.trim())} onClick={() => update(true)}>
            {resolve ? 'Send for review' : 'Approve and close'}
          </button>
        </div>
      </>}
    </div>
  </WorkPage>;
}

export function CorrectiveActionsPage({ workspace, canManage, openInspection, revision, actionId }) {
  const [updates, setUpdates] = useState(0);
  const [filter, setFilter] = useState('All');
  const { data, error } = useWorkspaceData(workspace, loadActions, revision + updates);
  const items = data?.filter(item => filter === 'All' || item.status === filter);
  const detail = data?.find(item => item.id === actionId);
  const reload = () => setUpdates(current => current + 1);
  if (actionId) return detail ? <ActionDetails key={detail.version} item={detail} workspace={workspace} canManage={canManage}
    onUpdate={reload} onReload={reload} onOpenInspection={openInspection} /> : <WorkPage title="Corrective action"
    back="#corrective-actions" backLabel="Back to corrective actions" layout="reading">
    {error ? <Feedback message={error} /> : data ? <Empty title="Action not found" /> : <Loading />}
  </WorkPage>;
  return <section className="panel team-panel">
    <div className="team-heading"><h2>{canManage ? 'Workspace corrective actions' : 'Corrective actions assigned to you'}</h2>
      <select aria-label="Filter corrective actions" value={filter} onChange={event => setFilter(event.target.value)}>
        <option value="All">All statuses</option>
        {Object.entries(labels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </select>
    </div>
    <Feedback message={error} />
    {items?.length ? <div className="assignment-list">{items.map(item => <a className="assignment-item corrective-item" key={item.id} href={routeLink('corrective-actions', item.id)}>
      <span><strong>{item.siteName}</strong><small>{item.finding}</small></span>
      {canManage && <span className="muted">{item.assignee}</span>}
      <span>Due {item.dueDate}</span><Badge status={item.status} label={labels[item.status]} />
    </a>)}</div> : items ? <Empty title="No matching actions">
      {canManage ? 'Open a submitted inspection and assign an action to a failed answer.' : 'Your assigned follow-up work will appear here.'}
    </Empty> : !error && <Loading />}
  </section>;
}
