"""Turns the twin's training records and commissioning reports into tidy CSVs for the analytics dashboard.

Reads:
  Builds/Training/lockout_*.csv           lockout runs from the Editor (desktop trainee or scripted)
  <persistentDataPath>/Training/*.csv     lockout runs from the Windows build (desktop or Quest 3)
  Builds/Commissioning/vc_*.csv           acceptance-suite reports (Unity sequencer or soft PLC)

Writes to Builds/Analytics/ (or --out):
  training_runs.csv   one row per lockout run
  training_steps.csv  one row per procedure step per run, with its time and mistakes
  commissioning.csv   one row per scenario per suite run

Usage: python tools/analytics/build_datasets.py [--vr-run lockout_20261027_151529.csv ...] [--out DIR]
A run from the build counts as a headset run when named with --vr-run (the record does not say how it was played).
"""
import argparse
import csv
import os
import re
from collections import defaultdict
from datetime import datetime

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BUILD_DATA = os.path.join(os.environ.get('USERPROFILE', ''), 'AppData', 'LocalLow', 'DefaultCompany', 'factory-twin-starter')
STEPS = 12


def training_files():
    for folder, origin in ((os.path.join(ROOT, 'Builds', 'Training'), 'editor'), (os.path.join(BUILD_DATA, 'Training'), 'build')):
        if os.path.isdir(folder):
            for name in sorted(os.listdir(folder)):
                if name.startswith('lockout_') and name.endswith('.csv'):
                    yield os.path.join(folder, name), origin


def mode_for(name, origin, rows, vr_runs):
    if name in vr_runs:
        return 'Quest 3 headset'
    # The Editor's scripted check-runs press a step every 1.5 s, faster than anyone does by hand.
    done = [float(r['at_s']) for r in rows if r['kind'] == 'done' and r['at_s']]
    gaps = [b - a for a, b in zip(done, done[1:])]
    if origin == 'editor' and gaps and max(gaps) < 7:
        return 'Desktop, scripted'
    return 'Desktop'


def training(vr_runs):
    runs, steps = [], []
    for path, origin in training_files():
        name = os.path.basename(path)
        with open(path, newline='', encoding='utf-8') as f:
            rows = list(csv.DictReader(f))
        events = [r for r in rows if r['kind'] in ('done', 'mistake')]
        summary = next((r for r in rows if r['kind'] not in ('done', 'mistake')), None)
        if not events or summary is None:
            continue
        mode = mode_for(name, origin, events, vr_runs)
        run_id = name[len('lockout_'):-len('.csv')]
        started = rows[0].get('started_utc', '')
        mistakes = sum(1 for r in events if r['kind'] == 'mistake')
        done_at = {int(r['step']): float(r['at_s']) for r in events if r['kind'] == 'done'}
        runs.append({
            'run': run_id, 'mode': mode, 'trainee': rows[0].get('trainee', ''), 'started_utc': started,
            'result': summary['kind'], 'steps_done': len(done_at), 'mistakes': mistakes,
            'duration_s': round(max(done_at.values()), 1) if done_at else '',
        })
        titles = {int(r['step']): r['detail'] for r in events if r['kind'] == 'done'}
        regulations = {int(r['step']): r['regulation'] for r in events if r['kind'] == 'done'}
        for step in range(1, STEPS + 1):
            at = done_at.get(step)
            before = done_at.get(step - 1, 0.0) if step > 1 else 0.0
            steps.append({
                'run': run_id, 'mode': mode, 'step': step, 'title': titles.get(step, ''),
                'regulation': regulations.get(step, ''),
                'done_at_s': at if at is not None else '',
                'step_s': round(at - before, 1) if at is not None else '',
                'mistakes': sum(1 for r in events if r['kind'] == 'mistake' and int(r['step']) == step),
            })
    return runs, steps


def commissioning():
    folder = os.path.join(ROOT, 'Builds', 'Commissioning')
    out = []
    if not os.path.isdir(folder):
        return out
    for name in sorted(os.listdir(folder)):
        m = re.match(r'vc_(unity|plc)_(\d{8})_(\d{6})\.csv$', name)
        if not m:
            continue
        run_at = datetime.strptime(m.group(2) + m.group(3), '%Y%m%d%H%M%S').strftime('%Y-%m-%d %H:%M')
        with open(os.path.join(folder, name), newline='', encoding='utf-8') as f:
            rows = list(csv.DictReader(f))
        order, outcome, failed = [], {}, defaultdict(list)
        for r in rows:
            s = r['scenario']
            if s not in outcome:
                order.append(s)
                outcome[s] = 'Pass'
            if r['outcome'] == 'FAIL':
                outcome[s] = 'Fail'
                failed[s].append(r['check'])
            elif r['outcome'] == 'n/a':
                outcome[s] = 'N/A'
        for i, s in enumerate(order, 1):
            out.append({
                'report': name[:-4], 'logic': 'Soft PLC' if m.group(1) == 'plc' else 'Unity sequencer',
                'run_at': run_at, 'program': rows[0].get('program', ''), 'scenario_no': i, 'scenario': s,
                'outcome': outcome[s], 'failed_checks': '; '.join(failed[s]),
            })
    return out


def write(path, rows):
    if not rows:
        return
    with open(path, 'w', newline='', encoding='utf-8') as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    print(f'{os.path.relpath(path, ROOT)}: {len(rows)} rows')


def main():
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('--vr-run', action='append', default=[], help='record file name of a run played in the headset')
    parser.add_argument('--out', default=os.path.join(ROOT, 'Builds', 'Analytics'))
    args = parser.parse_args()
    os.makedirs(args.out, exist_ok=True)
    runs, steps = training(set(args.vr_run))
    write(os.path.join(args.out, 'training_runs.csv'), runs)
    write(os.path.join(args.out, 'training_steps.csv'), steps)
    write(os.path.join(args.out, 'commissioning.csv'), commissioning())


if __name__ == '__main__':
    main()
