export type Role = "Manager" | "Specialist";

export interface CurrentUser {
  username: string;
  displayName: string;
  role: Role;
}

export interface Specialist {
  username: string;
  displayName: string;
}

export interface WorklistRow {
  claimId: string;
  patientName: string;
  payer: string;
  deniedAmount: number;
  bucket: string;
  deadline: string | null;
  daysToDeadline: number | null;
  expectedValue: number;
  priorityScore: number;
  rootCause: string;
  owningTeam: string;
  action: string;
  confidence: string;
  assignedTo: string | null;
  status: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface WorklistPage extends PagedResult<WorklistRow> {
  totalDenied: number;
}

export interface DenialAnalysis {
  claimId: string;
  payerId: string;
  status: string;
  deniedAmount: number;
  denialDate: string | null;
  reasonCodes: string;
  rootCause: string;
  owningTeam: string;
  preventable: string;
  action: string;
  nextAction: string;
  citations: string[];
  confidence: string;
  confidenceReason: string | null;
  bucket: string;
  deadline: string | null;
  daysToDeadline: number | null;
  expectedValue: number;
  priorityScore: number;
}

export interface DenialPrediction {
  rootCause: string;
  owningTeam: string;
  preventable: string;
}

export interface DraftResult {
  source: string;
  status: string;
  statusReason: string | null;
  letter: string | null;
  citations: string[];
  aiPrediction: DenialPrediction | null;
  aiConfidence: string | null;
}

export interface DenialDetail {
  analysis: DenialAnalysis;
  draft: DraftResult | null;
}

export interface ClaimLine {
  lineNo: number;
  cpt: string;
  modifier: string;
  units: number;
  charge: number;
  diagnosisCodes: string[];
  authNumber: string;
}

export interface DenialLine {
  procedureCode: string;
  group: string;
  reason: string;
  amount: number;
  remarks: string[];
}

export interface RemitEvent {
  paymentDate: string;
  statusCode: string;
  charge: number;
  paid: number;
  payerName: string;
  traceNumber: string;
  sourceFile: string;
}

export interface WorklogEntry {
  rowNumber: number;
  loggedDate: string | null;
  notes: string;
  owner: string | null;
  status: string;
  statusRaw: string;
  suspiciousText: boolean;
}

export interface IngestionIssue {
  key: string;
  kind: string;
  severity: string;
  source: string;
  reference: string;
  reason: string;
  claimId: string | null;
  amount: number | null;
}

export interface ExceptionsPage extends PagedResult<IngestionIssue> {
  kinds: string[];
}

export interface ClaimDetail {
  claim: {
    claimId: string;
    patientFirst: string;
    patientLast: string;
    patientDob: string;
    memberId: string;
    payer: string;
    payerId: string;
    dateOfService: string;
    submittedDate: string;
    renderingNpi: string;
    renderingProvider: string;
    facility: string;
    placeOfService: string;
    coderId: string;
    prebillReviewed: boolean;
    totalCharge: number;
  };
  lines: ClaimLine[];
  state: {
    status: string;
    billedCharge: number;
    netPaid: number;
    deniedAmount: number;
    denialDate: string | null;
    lastRemitDate: string | null;
    wasRecouped: boolean;
    possibleOverpayment: number;
    eventCount: number;
  };
  denialLines: DenialLine[];
  events: RemitEvent[];
  worklog: WorklogEntry[];
  issues: IngestionIssue[];
}

export interface WorkItem {
  claimId: string;
  assignedTo: string | null;
  status: string;
  updatedAt: string;
  updatedBy: string;
}

export interface WorkNote {
  id: number;
  author: string;
  text: string;
  createdAt: string;
}

export interface AuditEntry {
  id: number;
  at: string;
  actor: string;
  entity: string;
  action: string;
  beforeJson: string | null;
  afterJson: string | null;
}

export interface WorkItemDetail {
  item: WorkItem;
  notes: WorkNote[];
  audit: AuditEntry[];
}

export interface ReviewItem {
  analysis: DenialAnalysis;
  reason: string;
}

export interface SourceFileSummary {
  fileName: string;
  claimPayments: number;
  newEvents: number;
  duplicateEvents: number;
  paymentTotal: number;
  newPaymentTotal: number;
  outcome: string;
}

export interface Reconciliation {
  payerFilesTotalIncludingDuplicates: number;
  duplicatePaymentsExcluded: number;
  payerFilesTotal: number;
  paidToKnownClaims: number;
  paidToUnmatched: number;
  providerLevelAdjustments: number;
  difference: number;
  claimsByStatus: Record<string, number>;
  files: SourceFileSummary[];
}

export interface PolicySection {
  policyTitle: string;
  text: string;
}

export interface Reference {
  reasonCodes: Record<string, string>;
  remarkCodes: Record<string, string>;
  policySections: Record<string, PolicySection>;
}

export interface BucketSummary {
  bucket: string;
  claims: number;
  deniedAmount: number;
  expectedValue: number;
}

export interface DenialSummary {
  openDenials: number;
  deniedAmount: number;
  expectedValue: number;
  buckets: BucketSummary[];
  dueWithin14Days: { claims: number; deniedAmount: number };
  dueWithin30Days: { claims: number; deniedAmount: number };
}

export interface AuthOptions {
  passwordReset: boolean;
}
