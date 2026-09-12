"""Smoke-test the combined CLI/MCP distribution without attaching to any process."""
import argparse
import json
from pathlib import Path
import socket
import subprocess
import time
import urllib.request


def main(directory):
    directory = directory.resolve()
    cli = directory / 'Overmem.Cli.exe'
    assert cli.is_file(), cli
    help_text = subprocess.check_output([str(cli), '--help'], text=True, encoding='utf-8', timeout=15)
    assert 'pes2021-discover-player-families' in help_text
    assert 'serve ' in help_text
    with socket.socket() as socket_probe:
        socket_probe.bind(('127.0.0.1', 0))
        port = socket_probe.getsockname()[1]
    # This process and its child belong exclusively to this smoke test.
    log_path = directory / 'distribution-smoke.log'
    with log_path.open('w', encoding='utf-8') as log:
        server = subprocess.Popen([str(cli), 'serve', '--transport', 'sse', '--port', str(port)],
            stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            deadline = time.monotonic() + 15
            while True:
                if server.poll() is not None:
                    raise RuntimeError('CLI serve exited unexpectedly; see ' + str(log_path))
                try:
                    with socket.create_connection(('127.0.0.1', port), timeout=.2):
                        break
                except OSError:
                    if time.monotonic() > deadline:
                        raise TimeoutError('HTTP MCP server did not start')
                    time.sleep(.1)
            headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
            def rpc(method, params, request_id):
                body = json.dumps({'jsonrpc': '2.0', 'id': request_id, 'method': method, 'params': params}).encode()
                request = urllib.request.Request(f'http://127.0.0.1:{port}/sse', body, headers, method='POST')
                with urllib.request.urlopen(request, timeout=10) as response:
                    if response.headers.get('Mcp-Session-Id'):
                        headers['Mcp-Session-Id'] = response.headers['Mcp-Session-Id']
                    if 'text/event-stream' in response.headers.get('Content-Type', ''):
                        while line := response.readline():
                            if line.startswith(b'data: '):
                                return json.loads(line[6:])
                    return json.load(response)
            initialized = rpc('initialize', {'protocolVersion':'2025-03-26','capabilities':{},
                'clientInfo':{'name':'overmem-distribution-check','version':'1.0'}}, 1)
            assert 'result' in initialized, initialized
            listed = rpc('tools/list', {}, 2)
            names = {tool['name'] for tool in listed['result']['tools']}
            expected = {'pes2021_discover_player_families', 'pes2021_scan_players',
                'pes2021_extract_competition_fixtures', 'pes2021_find_daily_calendar_base_by_date'}
            assert expected <= names, expected - names
            print(json.dumps({'cliHelp': 'PASS', 'cliServeHttpMcp': 'PASS', 'tools': len(names), 'processAttachments': 0}))
        finally:
            if server.poll() is None:
                subprocess.run(['taskkill', '/PID', str(server.pid), '/T', '/F'], check=True, capture_output=True)
            server.wait(timeout=10)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('directory', type=Path, nargs='?', default=Path(__file__).resolve().parents[1] / 'artifacts/distribution')
    main(parser.parse_args().directory)
