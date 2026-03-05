import { describe, it, expect } from 'vitest';
import {
  fetchApps,
  fetchDevices,
  fetchHealth,
  fetchVisualizerState,
  updateVisualizerSettings,
  setVisualizerHub75,
} from '../lib/api';

describe('api module', () => {
  it('exports fetchDevices function', () => {
    expect(typeof fetchDevices).toBe('function');
  });

  it('exports fetchApps function', () => {
    expect(typeof fetchApps).toBe('function');
  });

  it('exports fetchHealth function', () => {
    expect(typeof fetchHealth).toBe('function');
  });

  it('exports visualizer API functions', () => {
    expect(typeof fetchVisualizerState).toBe('function');
    expect(typeof updateVisualizerSettings).toBe('function');
    expect(typeof setVisualizerHub75).toBe('function');
  });
});
