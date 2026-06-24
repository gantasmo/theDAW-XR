'use strict';
/*
 * Quest MIDI Bridge
 * -----------------
 * A two-way bridge between the Quest Unity app and your WebMIDI DAW.
 *
 *   OUT  Quest app --TCP--> (adb reverse over USB) --> bridge --> loopMIDI "QuestMIDI"      --> DAW
 *   IN   DAW --> loopMIDI "QuestMIDI-Return" --> bridge --> (adb reverse over USB) --TCP--> Quest app
 *
 * The IN path completes the circuit: the DAW (or anything) writes MIDI to the
 * return loopMIDI port and the bridge forwards it down the same socket to the
 * headset, where the MIDI Reactor (and any other receiver) reacts to it.
 *
 * Use a SEPARATE loopMIDI port for the return ("QuestMIDI-Return"). Routing
 * the return through the same "QuestMIDI" port would echo the Quest's own
 * hand data straight back to it.
 *
 * Settings live in bridge-config.json (written by the Unity Setup Wizard).
 */

const fs = require('fs');
const net = require('net');
const path = require('path');
const { execSync } = require('child_process');

const CONFIG_PATH = path.join(__dirname, 'bridge-config.json');
const defaults = {
  tcpPort: 8765,
  midiPortName: 'QuestMIDI',            // OUT: Quest -> this port -> DAW
  midiInPortName: 'QuestMIDI-Return',   // IN:  DAW  -> this port -> Quest (return circuit)
  adbPath: 'adb',
  autoAdbReverse: true,
  verbose: false
};

let cfg = Object.assign({}, defaults);
try {
  cfg = Object.assign(cfg, JSON.parse(fs.readFileSync(CONFIG_PATH, 'utf8')));
} catch (e) {
  console.log('[CONFIG] bridge-config.json not found - using defaults.');
}

console.log('');
console.log('===============================================');
console.log('   Quest MIDI Bridge');
console.log('===============================================');
console.log('  TCP port        : ' + cfg.tcpPort);
console.log('  MIDI out (->DAW): ' + cfg.midiPortName);
console.log('  MIDI in  (->Quest): ' + cfg.midiInPortName);
console.log('');

let midi;
try {
  midi = require('@julusian/midi');
} catch (e) {
  console.error('[ERROR] Could not load the MIDI module (@julusian/midi).');
  console.error('        Run  npm install  in this folder first.');
  process.exit(1);
}

// 1) USB tunnel: maps the Quest device localhost:port to this PC, over the cable.
if (cfg.autoAdbReverse) {
  const adb = (cfg.adbPath && String(cfg.adbPath).trim()) ? cfg.adbPath : 'adb';
  try {
    execSync('"' + adb + '" reverse tcp:' + cfg.tcpPort + ' tcp:' + cfg.tcpPort, { stdio: 'pipe' });
    console.log('[USB] adb reverse OK  (Quest 127.0.0.1:' + cfg.tcpPort + ' -> this PC)');
  } catch (e) {
    console.log('[USB] adb reverse FAILED - the bridge will still listen.');
    console.log('      Fix: connect the Quest by USB-C, accept the Allow USB debugging prompt,');
    console.log('      and make sure adb is installed (Android platform-tools).');
  }
}

// 2) Open the virtual MIDI output (loopMIDI). Retries until the port appears,
//    so you can create the loopMIDI port while this is running.
const output = new midi.Output();
let openedName = null;

function tryOpenMidi() {
  const count = output.getPortCount();
  for (let i = 0; i < count; i++) {
    const pn = output.getPortName(i);
    if (pn.toLowerCase().indexOf(String(cfg.midiPortName).toLowerCase()) !== -1) {
      output.openPort(i);
      openedName = pn;
      return true;
    }
  }
  return false;
}

function listPorts() {
  const count = output.getPortCount();
  if (count === 0) { console.log('         (no MIDI output ports found)'); return; }
  for (let i = 0; i < count; i++) console.log('         - ' + output.getPortName(i));
}

if (tryOpenMidi()) {
  console.log('[MIDI] Sending to: ' + openedName);
} else {
  console.log('[MIDI] No port matching "' + cfg.midiPortName + '" yet.');
  console.log('[MIDI] Open loopMIDI and create a port with that name. Current output ports:');
  listPorts();
  const timer = setInterval(function () {
    if (tryOpenMidi()) {
      clearInterval(timer);
      console.log('[MIDI] Sending to: ' + openedName);
    }
  }, 2000);
}

// 2b) Return circuit: open a SECOND virtual MIDI port as an INPUT. Whatever the
//     DAW writes there gets forwarded to every connected Quest socket using the
//     same [len][bytes...] framing, so the headset (MIDI Reactor, etc.) reacts.
const sockets = new Set();

function frameMidi(msg) {
  const len = Math.min(msg.length, 255);
  const out = Buffer.allocUnsafe(len + 1);
  out[0] = len;
  for (let i = 0; i < len; i++) out[i + 1] = msg[i] & 0xff;
  return out;
}

const input = new midi.Input();
let inOpenedName = null;
// Keep noisy realtime messages (clock / active-sensing) off the cable; pass
// note / CC / pitch-bend / program-change. (sysex, timing, activeSensing) ignored.
try { input.ignoreTypes(true, true, true); } catch (e) {}
input.on('message', function (_deltaTime, msg) {
  if (!msg || msg.length === 0) return;
  const frame = frameMidi(msg);
  for (const s of sockets) {
    try { s.write(frame); } catch (e) { /* dropped; socket cleanup handles it */ }
  }
  if (cfg.verbose) {
    console.log('[IN ] ' + msg.map(function (b) { return ('0' + b.toString(16)).slice(-2); }).join(' '));
  }
});

function tryOpenInput() {
  const count = input.getPortCount();
  for (let i = 0; i < count; i++) {
    const pn = input.getPortName(i);
    if (pn.toLowerCase().indexOf(String(cfg.midiInPortName).toLowerCase()) !== -1) {
      input.openPort(i);
      inOpenedName = pn;
      return true;
    }
  }
  return false;
}

if (tryOpenInput()) {
  console.log('[MIDI] Return input from: ' + inOpenedName);
} else {
  console.log('[MIDI] No return port matching "' + cfg.midiInPortName + '" yet.');
  console.log('[MIDI] Create a second loopMIDI port with that name to drive the headset from the DAW.');
  const inTimer = setInterval(function () {
    if (tryOpenInput()) {
      clearInterval(inTimer);
      console.log('[MIDI] Return input from: ' + inOpenedName);
    }
  }, 2000);
}

// 3) TCP server: receive length-prefixed MIDI frames -> [len][bytes...]
const server = net.createServer(function (socket) {
  socket.setNoDelay(true);
  sockets.add(socket);
  console.log('[NET] Quest connected (' + socket.remoteAddress + ':' + socket.remotePort + ')');

  let buf = Buffer.alloc(0);
  socket.on('data', function (chunk) {
    buf = buf.length ? Buffer.concat([buf, chunk]) : chunk;
    let off = 0;
    while (off < buf.length) {
      const len = buf[off];
      if (off + 1 + len > buf.length) break; // wait for the rest of the frame
      if (len > 0) {
        const msg = Array.prototype.slice.call(buf, off + 1, off + 1 + len);
        if (openedName) {
          try { output.sendMessage(msg); } catch (e) { /* ignore malformed */ }
        }
        if (cfg.verbose) {
          console.log('[MIDI] ' + msg.map(function (b) { return ('0' + b.toString(16)).slice(-2); }).join(' '));
        }
      }
      off += 1 + len;
    }
    buf = off > 0 ? buf.subarray(off) : buf;
  });

  socket.on('close', function () { sockets.delete(socket); console.log('[NET] Quest disconnected'); });
  socket.on('error', function (e) { sockets.delete(socket); console.log('[NET] socket error: ' + e.message); });
});

server.on('error', function (e) {
  console.error('[NET] server error: ' + e.message);
  if (e.code === 'EADDRINUSE') {
    console.error('       Port ' + cfg.tcpPort + ' is already in use (is another bridge running?).');
  }
  process.exit(1);
});

server.listen(cfg.tcpPort, '127.0.0.1', function () {
  console.log('[NET] Listening on 127.0.0.1:' + cfg.tcpPort + ' - waiting for the Quest app...');
  console.log('');
  console.log('Ready. Keep this window open during your session. Press Ctrl+C to stop.');
  console.log('');
});

process.on('SIGINT', function () {
  try { output.closePort(); } catch (e) {}
  try { input.closePort(); } catch (e) {}
  console.log('\nBridge stopped.');
  process.exit(0);
});
