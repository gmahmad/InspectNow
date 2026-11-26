import { useEffect, useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import {
  Building2, Check, ChevronDown, ChevronLeft, ChevronRight, CircleHelp,
  ClipboardCheck, ClipboardList, Clock3, FileCheck2, FileText, LayoutDashboard, Link2,
  ListChecks, LogIn, MapPin, Menu, Plus, Search, ShieldCheck, Users, Wrench, X,
} from 'lucide-react';
import { Badge, Connect, CreateRecord, Empty, Feedback, InspectionEditor, Loading, Modal, Progress, TemplateEditor, WorkPage } from './components.jsx';
import { createApiWorkspace, createDemoWorkspace, statusLabel } from './workspace.js';
import { AccountForm, AssignmentForm, AssignmentsPage, SitesPage, TeamPage, WorkspacePicker, workspaceTypes } from './team.jsx';
import { CorrectiveActionForm, CorrectiveActionsPage } from './corrective.jsx';
import { FormsPage, PublicFormPage } from './forms.jsx';
import { recordId, routeFromHash, routeLink } from './routes.js';
import './styles.css';

const navigation = [
  { id: 'overview', label: 'Overview', icon: LayoutDashboard },
  { id: 'forms', label: 'Forms', icon: FileText },
  { id: 'assessments', label: 'Assessments', icon: FileCheck2 },
  { id: 'course-feedback', label: 'Course feedback', icon: ListChecks },
  { id: 'client-intake', label: 'Client intake', icon: Users },
  { id: 'service-requests', label: 'Service requests', icon: ClipboardCheck },
  { id: 'inspections', label: 'Inspections', icon: ClipboardList },
  { id: 'checklists', label: 'Checklists', icon: ListChecks },
  { id: 'assignments', label: 'Assignments', icon: ClipboardCheck },
  { id: 'corrective-actions', label: 'Corrective actions', icon: Wrench },
  { id: 'sites', label: 'Sites', icon: MapPin },
  { id: 'team', label: 'Team', icon: Users },
];
const titles = {
  overview: ['Workspace overview', 'Track inspections, manage checklists, and see what needs attention.'],
  forms: ['Forms and responses', 'Build reusable forms and find submitted records in one place.'],
  assessments: ['Assessments', 'Create knowledge checks and review submitted answers.'],
  'course-feedback': ['Course feedback', 'Collect learner feedback and review completed surveys.'],
  'client-intake': ['Client intake', 'Collect consistent client information and review submitted records.'],
  'service-requests': ['Service requests', 'Collect requests and keep a record of what clients need.'],
  inspections: ['Inspections', 'Review completed inspections or pick up work in progress.'],
  checklists: ['Checklists', 'Create and publish reusable checklists for your inspections.'],
  assignments: ['Assignments', 'See assigned work, due dates, and submitted results.'],
  'corrective-actions': ['Corrective actions', 'Track follow-up work for failed inspection answers.'],
  sites: ['Sites', 'Manage the locations your team inspects.'],
  team: ['Team', 'See who belongs to this workspace and invite new members.'],
};
const formRoutes = { forms: '', assessments: 'Assessment', 'course-feedback': 'Survey', 'client-intake': 'Intake', 'service-requests': 'Request' };
const typeRoutes = { Education: ['assessments', 'course-feedback'], ClientServices: ['client-intake', 'service-requests'] };
const inspectionRoutes = ['overview', 'inspections', 'checklists', 'assignments', 'corrective-actions', 'sites'];
function landingFor(organization) {
  const tailored = typeRoutes[organization.type]?.[0];
  if (tailored && organization.canUseForms) return tailored;
  if (organization.role === 'Inspector' && organization.canUseInspections) return 'assignments';
  if (organization.type === 'FieldOperations' && organization.canUseInspections) return 'overview';
  return 'forms';
}
const shortDate = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
const longDate = new Intl.DateTimeFormat(undefined, { weekday: 'long', month: 'short', day: 'numeric' });

function currentPage() {
  return routeFromHash(location.hash);
}

function Sidebar({ page, items, organization, typeName, isDemo, open, close, connect, accountAction, signedIn, help }) {
  return <>
    {open && <button className="sidebar-scrim" aria-label="Close navigation" onClick={close} />}
    <aside className={`sidebar ${open ? 'visible' : ''}`}>
      <a className="brand" href="#forms" onClick={close}>
        <span><ClipboardCheck size={24} strokeWidth={2.3} /></span>InspectNow
      </a>
      <button className="workspace-button" onClick={accountAction}>
        <span className="workspace-icon"><Building2 size={18} /></span>
        <span><strong>{organization}</strong><small>{isDemo ? 'Demo workspace' : typeName}</small></span>
        <ChevronDown size={15} className="workspace-chevron" />
      </button>
      <nav aria-label="Main navigation">
        <div className="nav-label">Workspace</div>
        {items.map(({ id, label, icon: Icon }) => <a href={`#${id}`} key={id} onClick={close} aria-current={page === id ? 'page' : undefined}>
          <Icon size={19} strokeWidth={1.85} />{label}
        </a>)}
      </nav>
      <div className="sidebar-footer">
        <button onClick={help}><CircleHelp size={19} /> Workflow guide</button>
        <button onClick={accountAction}><LogIn size={19} /> {signedIn ? 'Switch workspace' : 'Sign in'}</button>
        {!signedIn && <button onClick={connect}><Link2 size={19} /> {isDemo ? 'Connect with API token' : 'Manage API token'}</button>}
        <div className="identity"><div className="avatar"><ShieldCheck size={18} /></div><div><strong>Forms workspace</strong><small>{isDemo ? 'Local demo' : 'InspectNow API'}</small></div></div>
      </div>
    </aside>
  </>;
}

function Overview({ inspections, templates, start, inspector }) {
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
        {inspector ? <a className="button hero-button" href="#assignments">Open my assignments</a> :
          <button className="button hero-button" onClick={start}><Plus size={18} /> Start an inspection</button>}
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
      {!records.length && start && <div className="empty-action"><button className="button primary" onClick={start}><Plus size={18} /> New inspection</button></div>}</>}
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

function RecordView({ kind, id, workspace, back, refresh, canManage, onCorrectiveAction, correctiveQuestionId }) {
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
  const failedQuestion = record?.status === 'Submitted' && record.questions.find(question =>
    question.id === correctiveQuestionId && question.answer === 'Fail');
  if (correctiveQuestionId) return <WorkPage title="Assign corrective action" description={record?.siteName} back={back}
    backLabel="Back to inspection" layout="reading">
    <Feedback message={error} />
    {record ? failedQuestion && canManage ? <div className="panel work-panel">
      <CorrectiveActionForm workspace={workspace} record={record} question={failedQuestion}
        onCreated={() => { refresh(); location.hash = 'corrective-actions'; }} />
    </div> : <Empty title="This answer is unavailable for follow-up" /> : !error && <Loading />}
  </WorkPage>;
  return <WorkPage title={record?.name ?? record?.siteName ?? (isTemplate ? 'Checklist' : 'Inspection')}
    description={record?.templateName} back={back} backLabel={isTemplate ? 'Back to checklists' : 'Back to list'} layout="reading">
    <Feedback message={error} />
    {record ? <div className="panel work-panel">
      <Editor record={record} workspace={workspace} reload={reload} canManage={canManage} onCorrectiveAction={onCorrectiveAction} />
    </div> : !error && <Loading />}
  </WorkPage>;
}

function App({ route }) {
  const [connection, setConnection] = useState(null);
  const [account, setAccount] = useState(null);
  const [checkingSession, setCheckingSession] = useState(true);
  const accountApi = useMemo(() => createApiWorkspace(), []);
  const [demo] = useState(() => createDemoWorkspace());
  const workspace = useMemo(() => connection ? createApiWorkspace(connection.token, connection.organization.id) : demo, [connection, demo]);
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const [dialog, setDialog] = useState(null);
  const [sidebar, setSidebar] = useState(false);
  const canManage = connection?.organization.role !== 'Inspector';
  const canUseForms = connection?.organization.canUseForms !== false;
  const canUseInspections = connection?.organization.canUseInspections !== false;
  const tailored = typeRoutes[connection?.organization.type] ?? [];
  const nav = navigation.filter(item => {
    if (Object.hasOwn(formRoutes, item.id)) return canUseForms && (item.id === 'forms' || tailored.includes(item.id));
    if (inspectionRoutes.includes(item.id) && !canUseInspections) return false;
    if (item.id === 'assignments' || item.id === 'corrective-actions') return Boolean(connection);
    if (item.id === 'team' || item.id === 'sites') return Boolean(connection) && canManage;
    return true;
  });
  const page = nav.some(item => item.id === route.page) ? route.page : nav[0]?.id ?? 'no-access';
  const parts = page === route.page ? route.parts : [];
  const [target, mode, targetId] = parts;
  const formPage = Object.hasOwn(formRoutes, page);
  const inspectionPage = inspectionRoutes.includes(page);
  const isRecordPage = ['inspections', 'assignments', 'checklists'].includes(page) && Boolean(recordId(target));
  const isCreatePage = ['inspections', 'assignments', 'checklists'].includes(page) && target === 'new' && canManage;
  const isDetailPage = isRecordPage || isCreatePage || (page === 'corrective-actions' && Boolean(recordId(target))) ||
    (formPage && ((target === 'view' && Boolean(recordId(mode))) || (target === 'new' && canManage) ||
      (target === 'responses' && Boolean(recordId(mode)) && canManage)));
  const refresh = () => setRevision(value => value + 1);
  const close = () => setDialog(null);
  const start = () => { location.hash = routeLink('inspections', 'new'); };
  const create = () => { location.hash = routeLink('checklists', 'new'); };
  const details = (kind, id, section = kind === 'template' ? 'checklists' : 'inspections') => { location.hash = routeLink(section, id); };
  const connect = () => { setSidebar(false); setDialog({ type: 'connect' }); };
  const accountAction = () => { setSidebar(false); setDialog({ type: account ? 'workspace' : 'account' }); };
  const changeConnection = value => {
    if (value !== connection) { setError(''); setData(null); setConnection(value); }
    close();
  };
  const selectOrganization = (organization, organizations) => {
    if (organizations) setAccount(current => ({ ...current, organizations }));
    changeConnection({ organization });
    sessionStorage.setItem('inspectnow.active-workspace', String(organization.id));
    location.hash = landingFor(organization);
  };
  async function finishSignIn() {
    const [current, organizations] = await Promise.all([accountApi.currentAccount(), accountApi.organizations()]);
    setAccount({ ...current, organizations });
    if (organizations.length === 1) selectOrganization(organizations[0]);
    else setDialog({ type: 'workspace' });
  }
  async function signOut() {
    await accountApi.signOut();
    sessionStorage.removeItem('inspectnow.active-workspace');
    setAccount(null);
    changeConnection(null);
    location.hash = 'overview';
  }

  useEffect(() => {
    let active = true;
    accountApi.currentAccount().then(async current => {
      const organizations = await accountApi.organizations();
      if (!active) return;
      setAccount({ ...current, organizations });
      const selected = organizations.find(item => String(item.id) === sessionStorage.getItem('inspectnow.active-workspace'))
        ?? (organizations.length === 1 ? organizations[0] : null);
      if (selected) {
        setData(null);
        setConnection({ organization: selected });
        if (!location.hash) location.hash = landingFor(selected);
      } else {
        setDialog({ type: 'workspace' });
      }
    }).catch(() => { /* Demo remains available when the API or session is unavailable. */ })
      .finally(() => { if (active) setCheckingSession(false); });
    return () => { active = false; };
  }, [accountApi]);

  useEffect(() => {
    if (!inspectionPage || checkingSession) return;
    let active = true;
    Promise.all([workspace.templates(), workspace.inspections()]).then(([templates, inspections]) => {
      if (active) { setData({ templates, inspections }); setError(''); }
    }, problem => { if (active) setError(problem.message); });
    return () => { active = false; };
  }, [workspace, revision, inspectionPage, checkingSession]);

  const [title, description] = titles[page] ?? ['Workspace access', 'Your workspace admin can grant access to forms or inspections.'];
  const organization = connection?.organization.name ?? 'Field Operations';
  return <div className="app-shell">
    <a className="skip" href="#main-content" onClick={event => { event.preventDefault(); document.getElementById('main-content').focus(); }}>Skip to main content</a>
    <Sidebar page={page} items={nav} organization={organization} isDemo={!connection} open={sidebar} close={() => setSidebar(false)} connect={connect}
      typeName={workspaceTypes[connection?.organization.type] ?? workspaceTypes.General}
      accountAction={accountAction} signedIn={Boolean(account)}
      help={() => { setSidebar(false); setDialog({ type: 'help' }); }} />
    <div className="content-shell">
      <header className="topbar"><div className="breadcrumbs"><button className="icon-button menu-button" aria-label="Open navigation" aria-expanded={sidebar} onClick={() => setSidebar(true)}><Menu size={22} /></button>
        <span>Workspace</span><ChevronRight size={15} /><strong>{navigation.find(item => item.id === page)?.label ?? 'Access'}</strong></div>
        <button className="connection-status" onClick={accountAction}><span />{connection ? connection.organization.role ?? 'Connected' : account ? 'Choose workspace' : 'Demo mode · Sign in'}<ChevronDown size={14} /></button>
      </header>
      <main id="main-content" tabIndex={-1} className="workspace-main">
        {!isDetailPage && <header className="page-heading"><div><h1>{title}</h1><p>{description}</p></div><div className="page-actions">
          {page === 'overview' && <span className="today">{longDate.format(new Date())}</span>}
          {canManage && ['overview', 'inspections', 'checklists'].includes(page) && <button className="button primary" disabled={!data} onClick={page === 'checklists' ? create : start}><Plus size={19} /> {page === 'checklists' ? 'New checklist' : 'New inspection'}</button>}
        </div></header>}
        {checkingSession && <Loading />}
        {!checkingSession && formPage && <FormsPage key={`${connection?.organization.id ?? 'demo'}-${page}`} workspace={workspace} canManage={canManage} startingKind={formRoutes[page]} page={page} parts={parts} routeQuery={route.query} />}
        {!checkingSession && page === 'team' && <TeamPage key={connection.organization.id} workspace={workspace} organization={connection.organization} canInvite={connection.organization.role === 'Admin'} canInviteAdmin={Boolean(account?.canProvision)} />}
        {inspectionPage && <Feedback message={error} />}
        {inspectionPage && error && <div className="retry"><button className="button secondary" onClick={refresh}>Try again</button></div>}
        {inspectionPage && !data && !error && !checkingSession && !isRecordPage && <Loading />}
        {!checkingSession && isRecordPage && <RecordView key={`${workspace === demo ? 'demo' : connection.organization.id}-${page}-${target}-${targetId ?? ''}`}
          workspace={workspace} kind={page === 'checklists' ? 'template' : 'inspection'} id={recordId(target)}
          back={mode === 'corrective' ? routeLink(page, target) : routeLink(page)} refresh={refresh} canManage={canManage}
          correctiveQuestionId={mode === 'corrective' ? recordId(targetId) : null}
          onCorrectiveAction={connection && canManage ? (record, question) => { location.hash = routeLink(page, record.id, 'corrective', question.id); } : undefined} />}
        {inspectionPage && data && !checkingSession && !isRecordPage && !isCreatePage && <>
          {page === 'overview' && <Overview inspections={data.inspections} templates={data.templates} start={start} inspector={!canManage} />}
          {['overview', 'inspections'].includes(page) && <InspectionList key={`${organization}-${page}`} records={data.inspections} overview={page === 'overview'} openRecord={id => details('inspection', id)} start={canManage ? start : undefined} />}
          {page === 'checklists' && <ChecklistList key={organization} templates={data.templates} openRecord={id => details('template', id)} />}
          {page === 'assignments' && <AssignmentsPage key={connection.organization.id} workspace={workspace} canManage={canManage} openRecord={id => details('inspection', id, 'assignments')} newAssignment={() => { location.hash = routeLink('assignments', 'new'); }} revision={revision} />}
          {page === 'corrective-actions' && <CorrectiveActionsPage key={connection.organization.id} workspace={workspace} canManage={canManage} revision={revision} actionId={recordId(target)} openInspection={id => details('inspection', id)} />}
          {page === 'sites' && <SitesPage key={connection.organization.id} workspace={workspace} />}
        </>}
        {isCreatePage && data && !checkingSession && <WorkPage title={page === 'assignments' ? 'New inspection assignment' : page === 'checklists' ? 'New checklist' : 'New inspection'}
          back={routeLink(page)} backLabel="Back to list" layout="reading">
          <div className="panel work-panel">
            {page === 'assignments' ? <AssignmentForm workspace={workspace} templates={data.templates} onCreated={id => { refresh(); details('inspection', id, 'assignments'); }} /> :
              <CreateRecord kind={page === 'checklists' ? 'template' : 'inspection'} workspace={workspace} templates={data.templates}
                onClose={() => { location.hash = routeLink(page); }} onCreated={id => { refresh(); details(page === 'checklists' ? 'template' : 'inspection', id); }} />}
          </div>
        </WorkPage>}
        <footer className="main-footer"><span>InspectNow</span><span>{connection ? `Connected to ${organization}` : 'Demo changes are saved in this browser'}</span></footer>
      </main>
    </div>
    {dialog?.type === 'account' && <Modal title="Sign in to InspectNow" onClose={close}><AccountForm api={accountApi} onSignedIn={finishSignIn} /></Modal>}
    {dialog?.type === 'workspace' && account && <Modal title="Choose your workspace" onClose={close}>
      <WorkspacePicker api={accountApi} account={account} onSelect={selectOrganization} onSignOut={signOut} />
    </Modal>}
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

function Root() {
  const [route, setRoute] = useState(currentPage);
  useEffect(() => {
    const update = () => {
      setRoute(currentPage());
      window.scrollTo(0, 0);
    };
    window.addEventListener('hashchange', update);
    return () => window.removeEventListener('hashchange', update);
  }, []);
  if (route.page === 'public-form') {
    const [key] = route.parts;
    if (route.parts.length !== 1 || !/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(key ?? ''))
      return <main className="public-form-page"><Empty title="This form link is invalid" /></main>;
    return <PublicFormPage key={key} publicKey={key} />;
  }
  return <App route={route} />;
}

createRoot(document.getElementById('root')).render(<Root />);
