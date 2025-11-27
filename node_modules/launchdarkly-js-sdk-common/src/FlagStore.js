const utils = require('./utils');

/**
 * FlagStore - Centralized flag store and access point for all feature flags
 *
 * This module manages two types of feature flags:
 * 1. Regular flags - Retrieved from LaunchDarkly servers or bootstrap data
 * 2. Override flags - Local overrides for debugging/testing
 *
 * When a flag is requested:
 * - If an override exists for that flag, the override value is returned
 * - Otherwise, the regular flag value is returned
 */
function FlagStore() {
  let flags = {};
  // The flag overrides are set lazily to allow bypassing property checks when no overrides are present.
  let flagOverrides;

  /**
   * Gets a single flag by key, with overrides taking precedence over regular flags
   * @param {string} key The flag key to retrieve
   * @returns {Object|null} The flag object or null if not found
   */
  function get(key) {
    // Check overrides first, then real flags
    if (flagOverrides && utils.objectHasOwnProperty(flagOverrides, key) && flagOverrides[key]) {
      return flagOverrides[key];
    }

    if (flags && utils.objectHasOwnProperty(flags, key) && flags[key] && !flags[key].deleted) {
      return flags[key];
    }

    return null;
  }

  /**
   * Gets all flags with overrides applied
   * @returns {Object} Object containing all flags with any overrides applied
   */
  function getFlagsWithOverrides() {
    const result = {};

    // Add all flags first
    for (const key in flags) {
      const flag = get(key);
      if (flag) {
        result[key] = flag;
      }
    }

    // Override with any flagOverrides (they take precedence)
    if (flagOverrides) {
      for (const key in flagOverrides) {
        const override = get(key);
        if (override) {
          result[key] = override;
        }
      }
    }

    return result;
  }

  /**
   * Replaces all flags with new flag data
   * @param {Object} newFlags - Object containing the new flag data
   */
  function setFlags(newFlags) {
    flags = { ...newFlags };
  }

  /**
   * Sets an override value for a specific flag
   * @param {string} key The flag key to override
   * @param {*} value The override value for the flag
   */
  function setOverride(key, value) {
    if (!flagOverrides) {
      flagOverrides = {};
    }
    flagOverrides[key] = { value };
  }

  /**
   * Removes an override for a specific flag
   * @param {string} key The flag key to remove the override for
   */
  function removeOverride(key) {
    if (!flagOverrides || !flagOverrides[key]) {
      return; // No override to remove
    }

    delete flagOverrides[key];

    // If no more overrides, reset to undefined for performance
    if (Object.keys(flagOverrides).length === 0) {
      flagOverrides = undefined;
    }
  }

  /**
   * Clears all flag overrides and returns the cleared overrides
   * @returns {Object} The overrides that were cleared, useful for tracking what was removed
   */
  function clearAllOverrides() {
    if (!flagOverrides) {
      return {}; // No overrides to clear, return empty object for consistency
    }

    const clearedOverrides = { ...flagOverrides };
    flagOverrides = undefined; // Reset to undefined
    return clearedOverrides;
  }

  /**
   * Gets the internal flag state without overrides applied
   * @returns {Object} The internal flag data structure
   */
  function getFlags() {
    return flags;
  }

  /**
   * Gets the flag overrides data
   * @returns {Object} The flag overrides object, or empty object if no overrides exist
   */
  function getFlagOverrides() {
    return flagOverrides || {};
  }

  return {
    clearAllOverrides,
    get,
    getFlagOverrides,
    getFlags,
    getFlagsWithOverrides,
    removeOverride,
    setFlags,
    setOverride,
  };
}

module.exports = FlagStore;
