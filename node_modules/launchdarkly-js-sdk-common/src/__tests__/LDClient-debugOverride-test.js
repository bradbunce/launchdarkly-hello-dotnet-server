const { initialize } = require('../index');
const stubPlatform = require('./stubPlatform');
const { respondJson } = require('./mockHttp');
const { makeBootstrap } = require('./testUtils');

// Mock the logger functions
const mockLogger = () => ({
  error: jest.fn(),
  warn: jest.fn(),
  info: jest.fn(),
  debug: jest.fn(),
});

// Define a basic Plugin structure for tests
const createTestPlugin = (name = 'Test Plugin') => {
  const plugin = {
    getMetadata: jest.fn().mockReturnValue({ name }),
    register: jest.fn(),
    getHooks: jest.fn().mockReturnValue([]),
    registerDebug: jest.fn(),
  };

  return plugin;
};

// Helper to initialize the client for tests
async function withClient(initialContext, configOverrides = {}, plugins = [], testFn) {
  const platform = stubPlatform.defaults();
  const server = platform.testing.http.newServer();
  const logger = mockLogger();

  // Disable streaming and event sending unless overridden
  const defaults = {
    baseUrl: server.url,
    streaming: false,
    sendEvents: false,
    useLdd: false,
    logger: logger,
    plugins: plugins,
  };
  const config = { ...defaults, ...configOverrides };
  const { client, start } = initialize('env', initialContext, config, platform);

  server.byDefault(respondJson({}));
  start();

  try {
    await client.waitForInitialization(10);
    await testFn(client, logger, platform);
  } finally {
    await client.close();
    server.close();
  }
}

describe('LDDebugOverride', () => {
  describe('setOverride method', () => {
    it('should set override value returned by variation method', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async client => {
        expect(client.variation('test-flag', 'default')).toBe('default');

        debugOverrideInterface.setOverride('test-flag', 'override-value');
        expect(client.variation('test-flag', 'default')).toBe('override-value');
      });
    });

    it('should override values taking precedence over real flag values from bootstrap', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      const flags = makeBootstrap({ 'existing-flag': { value: 'real-value', version: 1 } });

      await withClient({ key: 'user-key', kind: 'user' }, { bootstrap: flags }, [mockPlugin], async client => {
        expect(client.variation('existing-flag', 'default')).toBe('real-value');

        debugOverrideInterface.setOverride('existing-flag', 'override-value');
        expect(client.variation('existing-flag', 'default')).toBe('override-value');

        debugOverrideInterface.removeOverride('existing-flag');
        expect(client.variation('existing-flag', 'default')).toBe('real-value');
      });
    });
  });

  describe('removeOverride method', () => {
    it('should remove individual override and revert to default', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async client => {
        debugOverrideInterface.setOverride('test-flag', 'override-value');
        expect(client.variation('test-flag', 'default')).toBe('override-value');

        debugOverrideInterface.removeOverride('test-flag');
        expect(client.variation('test-flag', 'default')).toBe('default');
      });
    });

    it('should remove only the specified override leaving others intact', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async client => {
        debugOverrideInterface.setOverride('flag1', 'value1');
        debugOverrideInterface.setOverride('flag2', 'value2');
        debugOverrideInterface.setOverride('flag3', 'value3');

        debugOverrideInterface.removeOverride('flag2');

        expect(client.variation('flag1', 'default')).toBe('value1');
        expect(client.variation('flag2', 'default')).toBe('default');
        expect(client.variation('flag3', 'default')).toBe('value3');

        const allOverrides = debugOverrideInterface.getAllOverrides();
        expect(allOverrides).toEqual({
          flag1: 'value1',
          flag3: 'value3',
        });
      });
    });

    it('should handle removing non-existent override without throwing error', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async client => {
        debugOverrideInterface.setOverride('existing-flag', 'value');

        // Should not throw error
        expect(() => {
          debugOverrideInterface.removeOverride('non-existent-flag');
        }).not.toThrow();

        // Existing override should remain
        expect(client.variation('existing-flag', 'default')).toBe('value');
        const allOverrides = debugOverrideInterface.getAllOverrides();
        expect(allOverrides).toEqual({ 'existing-flag': 'value' });
      });
    });

    it('should be callable multiple times on same flag key safely', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async client => {
        debugOverrideInterface.setOverride('test-flag', 'value');

        debugOverrideInterface.removeOverride('test-flag');
        expect(client.variation('test-flag', 'default')).toBe('default');

        // Removing again should not cause issues
        expect(() => {
          debugOverrideInterface.removeOverride('test-flag');
        }).not.toThrow();

        expect(debugOverrideInterface.getAllOverrides()).toEqual({});
      });
    });
  });

  describe('clearAllOverrides method', () => {
    it('should clear all overrides and revert all flags to their default values', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async client => {
        debugOverrideInterface.setOverride('flag1', 'value1');
        debugOverrideInterface.setOverride('flag2', 'value2');

        debugOverrideInterface.clearAllOverrides();
        expect(client.variation('flag1', 'default')).toBe('default');
        expect(client.variation('flag2', 'default')).toBe('default');
        expect(debugOverrideInterface.getAllOverrides()).toEqual({});
      });
    });

    it('should operate safely when no overrides exist', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async () => {
        // Should not throw error when no overrides exist
        expect(() => {
          debugOverrideInterface.clearAllOverrides();
        }).not.toThrow();

        expect(debugOverrideInterface.getAllOverrides()).toEqual({});
      });
    });
  });

  describe('getAllOverrides method', () => {
    it('should return all current overrides', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async () => {
        debugOverrideInterface.setOverride('test-flag', 'override-value');

        const allOverrides = debugOverrideInterface.getAllOverrides();
        expect(allOverrides).toEqual({ 'test-flag': 'override-value' });
      });
    });

    it('should return empty object when no overrides have been set', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async () => {
        const allOverrides = debugOverrideInterface.getAllOverrides();
        expect(allOverrides).toEqual({});
        expect(typeof allOverrides).toBe('object');
        expect(Array.isArray(allOverrides)).toBe(false);
      });
    });

    it('should return immutable copy not reference to internal state', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async client => {
        debugOverrideInterface.setOverride('test-flag', 'original-value');

        const overrides1 = debugOverrideInterface.getAllOverrides();
        const overrides2 = debugOverrideInterface.getAllOverrides();

        // Should be different objects
        expect(overrides1).not.toBe(overrides2);

        // Modifying returned object should not affect internal state
        overrides1['new-flag'] = 'new-value';
        delete overrides1['test-flag'];

        expect(client.variation('test-flag', 'default')).toBe('original-value');
        expect(client.variation('new-flag', 'default')).toBe('default');

        const overrides3 = debugOverrideInterface.getAllOverrides();
        expect(overrides3).toEqual({ 'test-flag': 'original-value' });
      });
    });

    it('should maintain consistency across different operations', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async () => {
        // Test consistency through various operations
        expect(debugOverrideInterface.getAllOverrides()).toEqual({});

        debugOverrideInterface.setOverride('flag1', 'value1');
        expect(debugOverrideInterface.getAllOverrides()).toEqual({ flag1: 'value1' });

        debugOverrideInterface.setOverride('flag2', 'value2');
        expect(debugOverrideInterface.getAllOverrides()).toEqual({ flag1: 'value1', flag2: 'value2' });

        debugOverrideInterface.removeOverride('flag1');
        expect(debugOverrideInterface.getAllOverrides()).toEqual({ flag2: 'value2' });

        debugOverrideInterface.setOverride('flag2', 'updated-value2');
        expect(debugOverrideInterface.getAllOverrides()).toEqual({ flag2: 'updated-value2' });

        debugOverrideInterface.clearAllOverrides();
        expect(debugOverrideInterface.getAllOverrides()).toEqual({});
      });
    });
  });

  describe('integration with client methods', () => {
    it('should work correctly with variationDetail method', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      await withClient({ key: 'user-key', kind: 'user' }, {}, [mockPlugin], async client => {
        debugOverrideInterface.setOverride('test-flag', 'override-value');

        const detail = client.variationDetail('test-flag', 'default');
        expect(detail.value).toBe('override-value');
      });
    });

    it('should include overrides in allFlags method output', async () => {
      let debugOverrideInterface;
      const mockPlugin = createTestPlugin('test-plugin');
      mockPlugin.registerDebug.mockImplementation(debugOverride => {
        debugOverrideInterface = debugOverride;
      });

      const flags = makeBootstrap({ 'real-flag': { value: 'real-value', version: 1 } });

      await withClient({ key: 'user-key', kind: 'user' }, { bootstrap: flags }, [mockPlugin], async client => {
        debugOverrideInterface.setOverride('override-flag', 'override-value');
        debugOverrideInterface.setOverride('real-flag', 'overridden-real-value');

        const allFlags = client.allFlags();
        expect(allFlags['real-flag']).toBe('overridden-real-value');
        expect(allFlags['override-flag']).toBe('override-value');
      });
    });
  });
});
