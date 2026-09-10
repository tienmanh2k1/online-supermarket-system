import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export const RUN_ID = 'UI-20260910-01';
export const ARTIFACTS_DIR = path.resolve(__dirname, '../../../artifacts/ui-full-test', RUN_ID);
export const SCREENSHOTS_DIR = path.join(ARTIFACTS_DIR, 'screenshots');

export interface TestExecutionRecord {
  run: string;
  build: string;
  caseId: string;
  variant: string;
  category: string;
  priority: 'P0' | 'P1' | 'P2';
  browserViewport: string;
  roleData: string;
  expected: string;
  actual: string;
  status: 'Pass' | 'Fail' | 'Blocked' | 'N/A' | 'Not run';
  bug?: string;
  evidence?: string;
  timestamp: string;
  notes?: string;
}

// Ensure directories exist
fs.mkdirSync(SCREENSHOTS_DIR, { recursive: true });

const LEDGER_FILE = path.join(ARTIFACTS_DIR, 'execution-ledger.json');

export function logTestRecord(record: Omit<TestExecutionRecord, 'run' | 'timestamp'>) {
  const fullRecord: TestExecutionRecord = {
    run: RUN_ID,
    timestamp: new Date().toISOString(),
    ...record,
  };

  let records: TestExecutionRecord[] = [];
  if (fs.existsSync(LEDGER_FILE)) {
    try {
      records = JSON.parse(fs.readFileSync(LEDGER_FILE, 'utf8'));
    } catch {
      records = [];
    }
  }

  // Remove existing entry for same caseId + variant if re-running
  records = records.filter(
    (r) => !(r.caseId === fullRecord.caseId && r.variant === fullRecord.variant)
  );
  records.push(fullRecord);

  fs.writeFileSync(LEDGER_FILE, JSON.stringify(records, null, 2), 'utf8');
  console.log(
    `[${fullRecord.status}] ${fullRecord.caseId}/${fullRecord.variant} - ${fullRecord.actual}`
  );
}

export function getAllRecords(): TestExecutionRecord[] {
  if (fs.existsSync(LEDGER_FILE)) {
    try {
      return JSON.parse(fs.readFileSync(LEDGER_FILE, 'utf8'));
    } catch {
      return [];
    }
  }
  return [];
}
