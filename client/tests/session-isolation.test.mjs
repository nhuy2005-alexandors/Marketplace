import assert from "node:assert/strict";
import test from "node:test";
import {
  emptyRecommendationSession,
  privateQueryKeys,
  privateQueryPrefixes,
  resetRecommendationSession,
} from "../src/api/sessionIsolation.ts";

const userIds = [1, 2];

function userKeys(userId) {
  return [
    privateQueryKeys.recommendations(userId),
    privateQueryKeys.cart(userId),
    privateQueryKeys.orders(userId, 1, 20),
    privateQueryKeys.wishlist(userId),
  ];
}

function cacheKey(key) {
  return JSON.stringify(key);
}

function clearUserCache(cache, userId) {
  const prefixes = privateQueryPrefixes(userId).map(cacheKey);
  for (const key of cache.keys()) {
    if (prefixes.some((prefix) => key === prefix || key.startsWith(`${prefix.slice(0, -1)},`))) {
      cache.delete(key);
    }
  }
}

function putUserData(cache, userId) {
  const payloads = ["recommendation", "cart", "orders", "wishlist"];
  for (const [index, key] of userKeys(userId).entries()) {
    cache.set(cacheKey(key), `${userId}:${payloads[index]}`);
  }
}

function simulateSwitches(sequence) {
  const cache = new Map();
  let currentUserId = sequence[0];
  let recommendationState = {
    ...emptyRecommendationSession(currentUserId),
    sessionSearches: [`${currentUserId} query`],
    viewedProductIds: [currentUserId * 10],
    activeRecommendations: [{ id: currentUserId * 100 }],
  };
  putUserData(cache, currentUserId);

  for (const nextUserId of sequence.slice(1)) {
    const previousUserId = currentUserId;
    recommendationState = resetRecommendationSession(recommendationState, nextUserId);
    clearUserCache(cache, previousUserId);
    currentUserId = nextUserId;

    assert.equal(recommendationState.ownerUserId, nextUserId);
    assert.deepEqual(recommendationState.sessionSearches, []);
    assert.deepEqual(recommendationState.viewedProductIds, []);
    assert.deepEqual(recommendationState.activeRecommendations, []);
    for (const key of userKeys(nextUserId)) {
      assert.equal(cache.get(cacheKey(key)), undefined);
    }

    putUserData(cache, nextUserId);
    for (const key of userKeys(nextUserId)) {
      assert.equal(cache.get(cacheKey(key)), `${nextUserId}:${key[0] === "recommendations" ? "recommendation" : key[0]}`);
    }
  }
}

test("A to B to A clears recommendation and private query state on each identity change", () => {
  simulateSwitches([1, 2, 1]);
});

test("B to A to B clears recommendation and private query state on each identity change", () => {
  simulateSwitches([2, 1, 2]);
});

test("private query keys are distinct for each user", () => {
  for (const root of ["recommendations", "cart", "orders", "wishlist"]) {
    const keyForA = userKeys(1).find((key) => key[0] === root);
    const keyForB = userKeys(2).find((key) => key[0] === root);
    assert.notDeepEqual(keyForA, keyForB);
  }
});
