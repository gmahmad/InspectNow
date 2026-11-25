import { useEffect, useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import {
  Building2, Check, ChevronDown, ChevronLeft, ChevronRight, CircleHelp,
  ClipboardCheck, ClipboardList, Clock3, FileCheck2, FileText, LayoutDashboard, Link2,
  ListChecks, Menu, Plus, Search, ShieldCheck, X,
} from 'lucide-react';
import { Badge, Connect, CreateRecord, Empty, Feedback, InspectionEditor, Loading, Modal, Progress, TemplateEditor } from './components.jsx';
import { createApiWorkspace, createDemoWorkspace, statusLabel } from './workspace.js';
import './styles.css';

const navigation = [
  { id: 'overview', label: 'Overview', icon: LayoutDashboard },
  { id: 'inspections', label: 'Inspections', icon: ClipboardList },
  { id: 'checklists', label: 'Checklists', icon: ListChecks },
];
const titles = {
  overview: ['Workspace overview', 'Track inspections, manage checklists, and see what needs attention.'],
  inspections: ['Inspections', 'Review completed inspections or pick up work in progress.'],
  checklists: ['Checklists', 'Create and publish reusable checklists for your inspections.'],
};
const shortDate = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
const longDate = new Intl.DateTimeFormat(undefined, { weekday: 'long', month: 'short', day: 'numeric' });

function currentPage() {
  const hash = location.hash.slice(1);
  return navigation.some(item => item.id === hash) ? hash : 'overview';
}

function Sidebar({ page, organization, isDemo, open, close, connect, help }) {
  return <>
    {open && <button className="sidebar-scrim" aria-label="Close navigation" onClick={close} />}
    <aside className={`sidebar ${open ? 'visible' : ''}`}>
      <a className="brand" href="#overview" onClick={close}>
        <span><ClipboardCheck size={24} strokeWidth={2.3} /></span>InspectNow
      </a>
      <button className="workspace-button" onClick={connect}>
        <span className="workspace-icon"><Building2 size={18} /></span>
        <span><strong>{organization}</strong><small>{isDemo ? 'Demo workspace' : 'Connected workspace'}</small></span>
        <ChevronDown size={15} className="workspace-chevron" />
      </button>
      <nav aria-label="Main navigation">
        <div className="nav-label">Workspace</div>
        {navigation.map(({ id, label, icon: Icon }) => <a href={`#${id}`} key={id} onClick={close} aria-current={page === id ? 'page' : undefined}>
          <Icon size={19} strokeWidth={1.85} />{label}
        </a>)}
      </nav>
      <div className="sidebar-footer">
        <button onClick={help}><CircleHelp size={19} /> Workflow guide</button>
        <button onClick={connect}><Link2 size={19} /> {isDemo ? 'Connect to API' : 'Manage connection'}</button>
        <div className="identity"><div className="avatar"><ShieldCheck size={18} /></div><div><strong>Inspection workspace</strong><small>{isDemo ? 'Local demo' : 'InspectNow API'}</small></div></div>
      </div>
    </aside>
  </>;
}

function Overview({ inspections, templates, start }) {
  const submitted = inspections.filter(item => item.status === 'Submitted').length;
  const stats = [
    { label: 'Total inspections', value: inspections.length, icon: ClipboardList, color: 'green', note: 'Across this workspace' },
    { label: 'In progress', value: inspections.length - submitted, icon: Clock3, color: 'amber', note: 'Open inspection records' },
    { label: 'Submitted', value: submitted, icon: ClipboardCheck, color: 'mint', note: 'Completed inspections' },
    { label: 'Published checklists', value: templates.filter(item => item.status === 'Published').length, icon: FileCheck2, color: 'violet', note: 'Ready to use' },
  ];
  return <>
    <section className="hero" aria-labelledby="hero-title">
      <div className="hero-content"><div className="hero-label"><ShieldCheck size={16} /> Inspection workspace</div>
        <h2 id="hero-title">Ready for your next inspection?</h2><p>Start with a published checklist, record your findings, and submit the completed inspection.</p>
        <button className="button hero-button" onClick={start}><Plus size={18} /> Start an inspection</button>
      </div>
      <div className="hero-art" aria-hidden="true">
        <div className="orbit orbit-outer" /><div className="orbit orbit-inner" />
        <div className="art-paper back-paper" />
        <div className="art-paper front-paper"><div className="paper-clip" /><div className="paper-heading" />
          <div className="paper-row"><Check /><span /></div><div className="paper-row"><Check /><span /></div><div className="paper-row last"><span /><span /></div>
        </div><div className="art-check"><Check size={26} strokeWidth={3} /></div>
      </div>
    </section>
    <section className="stats" aria-label="Workspace totals">
      {stats.map(({ label, value, icon: Icon, color, note }) => <article className="stat" key={label}>
        <span className={`stat-icon ${color}`}><Icon size={20} /></span><div className="stat-label">{label}</div>
        <strong>{value}</strong><small>{note}</small>
      </article>)}
    </section>
  </>;
}

function InspectionList({ records, overview, openRecord, start }) {
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState('All');
  const [page, setPage] = useState(1);
  const size = overview ? 5 : 10;
  const term = query.trim().toLowerCase();
  const filtered = records.filter(item => (filter === 'All' || item.status === filter) &&
    `${item.siteName} ${item.templateName} ${item.id}`.toLowerCase().includes(term));
  const pages = Math.max(1, Math.ceil(filtered.length / size));
  const activePage = Math.min(page, pages);
  const current = filtered.slice((activePage - 1) * size, activePage * size);
  return <section className="panel inspection-panel" aria-label="Inspection list">
    <div className="panel-heading"><div><h2>{overview ? 'Recent inspections' : 'All inspections'}</h2>
      <p>{overview ? 'Pick up where you left off.' : `${records.length} inspection${records.length === 1 ? '' : 's'} in this workspace`}</p></div>
      {overview && <a className="button secondary small" href="#inspections">View all</a>}</div>
    <div className="table-tools">
      <label className="search"><Search size={18} /><input value={query} placeholder="Search inspections…" aria-label="Search inspections" onChange={event => { setQuery(event.target.value); setPage(1); }} />
        {query && <button className="clear" onClick={() => { setQuery(''); setPage(1); }} aria-label="Clear search"><X size={15} /></button>}</label>
      <select aria-label="Filter by inspection status" value={filter} onChange={event => { setFilter(event.target.value); setPage(1); }}>
        <option value="All">All statuses</option><option value="Draft">In progress</option><option value="Submitted">Submitted</option>
      </select>
    </div>
    {current.length ? <>
      <div className="table-scroll"><table><thead><tr><th>Inspection</th><th>Status</th><th>Progress</th><th>Started</th><th aria-label="Open inspection" /></tr></thead>
        <tbody>{current.map(item => <tr key={item.id}>
          <td><button className="record-link" onClick={() => openRecord(item.id)}><span className="row-icon"><ClipboardList size={20} /></span>
            <span><strong>{item.siteName}</strong><small>{item.templateName}</small></span></button></td>
          <td><Badge status={item.status} label={statusLabel(item.status)} /></td><td><Progress inspection={item} /></td>
          <td className="record-date">{shortDate.format(new Date(item.startedAtUtc))}</td>
          <td><button className="icon-button row-open" onClick={() => openRecord(item.id)} aria-label={`Open ${item.siteName}`}><ChevronRight size={19} /></button></td>
        </tr>)}</tbody></table></div>
      <div className="table-footer"><span>Showing {(activePage - 1) * size + 1}–{Math.min(activePage * size, filtered.length)} of {filtered.length}</span>
        {pages > 1 && <div><button className="icon-button" aria-label="Previous page" disabled={activePage === 1} onClick={() => setPage(activePage - 1)}><ChevronLeft size={18} /></button>
          <span>{activePage} / {pages}</span><button className="icon-button" aria-label="Next page" disabled={activePage === pages} onClick={() => setPage(activePage + 1)}><ChevronRight size={18} /></button></div>}</div>
    </> : <><Empty title={records.length ? 'No matching inspections' : 'No inspections yet'}>{records.length ? 'Try a different search or status.' : 'Start an inspection from a published checklist.'}</Empty>
      {!records.length && <div className="empty-action"><button className="button primary" onClick={start}><Plus size={18} /> New inspection</button></div>}</>}
  </section>;
}

function ChecklistList({ templates, openRecord }) {
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState('All');
  const visible = templates.filter(t => (filter === 'All' || t.status === filter) && t.name.toLowerCase().includes(query.trim().toLowerCase()));
  return <section className="panel checklist-panel" aria-label="Checklist library">
    <div className="panel-heading"><div><h2>Checklist library</h2><p>{templates.length} checklist{templates.length === 1 ? '' : 's'} in this workspace</p></div></div>
    <div className="table-tools"><label className="search"><Search size={18} /><input aria-label="Search checklists" placeholder="Search checklists…" value={query} onChange={e => setQuery(e.target.value)} /></label>
      <select aria-label="Filter by checklist status" value={filter} onChange={e => setFilter(e.target.value)}><option value="All">All statuses</option><option>Published</option><option>Draft</option></select></div>
    {visible.length ? <div className="checklist-grid">{visible.map(template => <button className="checklist-card" key={template.id} onClick={() => openRecord(template.id)}>
      <div><span className="stat-icon green"><ListChecks size={22} /></span><Badge status={template.status} /></div>
      <strong>{template.name}</strong><small>{template.questionCount ?? template.questions.length} questions</small>
      <span className="card-action">{template.status === 'Draft' ? 'Edit checklist' : 'View checklist'} <ChevronRight size={16} /></span>
    </button>)}</div> : <Empty icon={FileText} title={templates.length ? 'No matching checklists' : 'No checklists yet'}>{templates.length ? 'Try a different search or status.' : 'Create a checklist to start defining inspection questions.'}</Empty>}
  </section>;
}

function RecordDialog({ kind, id, workspace, onClose, refresh }) {
  const [record, setRecord] = useState(null);
  const [error, setError] = useState('');
  const isTemplate = kind === 'template';
  const load = () => workspace[kind](id);
  useEffect(() => {
    let active = true;
    workspace[kind](id).then(item => { if (active) setRecord(item); }, problem => { if (active) setError(problem.message); });
    return () => { active = false; };
  }, [id, kind, workspace]);
  async function reload() { setRecord(await load()); refresh(); }
  const Editor = isTemplate ? TemplateEditor : InspectionEditor;
  return <Modal title={record?.name ?? record?.siteName ?? (isTemplate ? 'Checklist' : 'Inspection')} description={record?.templateName} onClose={onClose} wide>
    <Feedback message={error} />{record ? <Editor record={record} workspace={workspace} reload={reload} /> : !error && <Loading />}
  </Modal>;
}

function App() {
  const [page, setPage] = useState(currentPage);
  const [connection, setConnection] = useState(null);
  const [demo] = useState(() => createDemoWorkspace());
  const workspace = useMemo(() => connection ? createApiWorkspace(connection.token, connection.organization.id) : demo, [connection, demo]);
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const [dialog, setDialog] = useState(null);
  const [sidebar, setSidebar] = useState(false);
  const refresh = () => setRevision(value => value + 1);
  const close = () => setDialog(null);
  const start = () => setDialog({ type: 'create', kind: 'inspection' });
  const create = () => setDialog({ type: 'create', kind: 'template' });
  const details = (kind, id) => setDialog({ type: 'record', kind, id });
  const connect = () => { setSidebar(false); setDialog({ type: 'connect' }); };
  const changeConnection = value => {
    if (value !== connection) { setError(''); setData(null); setConnection(value); }
    close();
  };

  useEffect(() => {
    const handle = () => setPage(currentPage());
    window.addEventListener('hashchange', handle);
    return () => window.removeEventListener('hashchange', handle);
  }, []);

  useEffect(() => {
    let active = true;
    Promise.all([workspace.templates(), workspace.inspections()]).then(([templates, inspections]) => {
      if (active) { setData({ templates, inspections }); setError(''); }
    }, problem => { if (active) setError(problem.message); });
    return () => { active = false; };
  }, [workspace, revision]);

  const [title, description] = titles[page];
  const organization = connection?.organization.name ?? 'Field Operations';
  return <div className="app-shell">
    <a className="skip" href="#main-content" onClick={event => { event.preventDefault(); document.getElementById('main-content').focus(); }}>Skip to main content</a>
    <Sidebar page={page} organization={organization} isDemo={!connection} open={sidebar} close={() => setSidebar(false)} connect={connect}
      help={() => { setSidebar(false); setDialog({ type: 'help' }); }} />
    <div className="content-shell">
      <header className="topbar"><div className="breadcrumbs"><button className="icon-button menu-button" aria-label="Open navigation" aria-expanded={sidebar} onClick={() => setSidebar(true)}><Menu size={22} /></button>
        <span>Workspace</span><ChevronRight size={15} /><strong>{navigation.find(item => item.id === page).label}</strong></div>
        <button className="connection-status" onClick={connect}><span />{connection ? 'API connected' : 'Demo mode'}<ChevronDown size={14} /></button>
      </header>
      <main id="main-content" tabIndex={-1} className="workspace-main">
        <header className="page-heading"><div><h1>{title}</h1><p>{description}</p></div><div className="page-actions">
          {page === 'overview' && <span className="today">{longDate.format(new Date())}</span>}
          <button className="button primary" disabled={!data} onClick={page === 'checklists' ? create : start}><Plus size={19} /> {page === 'checklists' ? 'New checklist' : 'New inspection'}</button>
        </div></header>
        <Feedback message={error} />
        {error && <div className="retry"><button className="button secondary" onClick={refresh}>Try again</button></div>}
        {!data && !error && <Loading />}
        {data && <>
          {page === 'overview' && <Overview inspections={data.inspections} templates={data.templates} start={start} />}
          {page === 'checklists' ? <ChecklistList key={organization} templates={data.templates} openRecord={id => details('template', id)} /> :
            <InspectionList key={`${organization}-${page}`} records={data.inspections} overview={page === 'overview'} openRecord={id => details('inspection', id)} start={start} />}
        </>}
        <footer className="main-footer"><span>InspectNow</span><span>{connection ? `Connected to ${organization}` : 'Demo changes are saved in this browser'}</span></footer>
      </main>
    </div>
    {dialog?.type === 'create' && <Modal title={dialog.kind === 'inspection' ? 'New inspection' : 'New checklist'} onClose={close}>
      <CreateRecord kind={dialog.kind} workspace={workspace} templates={data?.templates ?? []} onClose={close} onCreated={id => { refresh(); details(dialog.kind, id); }} />
    </Modal>}
    {dialog?.type === 'record' && <RecordDialog key={`${dialog.kind}-${dialog.id}`} {...dialog} workspace={workspace} onClose={close} refresh={refresh} />}
    {dialog?.type === 'connect' && <Modal title="Connect to InspectNow" description="Use an access token from your configured sign-in provider to open an organization." onClose={close}>
      <Connect connection={connection} onConnect={changeConnection} onDemo={() => changeConnection(null)} />
    </Modal>}
    {dialog?.type === 'help' && <Modal title="Inspection workflow" onClose={close}>
      <ol className="help-steps"><li><strong>Publish a checklist</strong><p>Create a checklist, add questions, then publish it for use in inspections.</p></li>
        <li><strong>Start at a site</strong><p>Select a published checklist and enter the site name.</p></li>
        <li><strong>Record findings and submit</strong><p>Save each answer. Required questions need a Pass or Fail before you can submit.</p></li></ol>
      <button className="button secondary" onClick={() => { close(); location.hash = 'checklists'; }}>Open checklists</button>
    </Modal>}
  </div>;
}

createRoot(document.getElementById('root')).render(<App />);
