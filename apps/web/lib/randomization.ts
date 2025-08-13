/**
 * Seeded random number generator using a simple Linear Congruential Generator (LCG)
 * This ensures consistent results for the same seed value.
 */
class SeededRandom {
  private seed: number;

  constructor(seed: number) {
    this.seed = seed;
  }

  /**
   * Generate next pseudo-random number between 0 and 1
   */
  next(): number {
    this.seed = (this.seed * 1664525 + 1013904223) % 4294967296;
    return this.seed / 4294967296;
  }
}

/**
 * Generate a numeric seed from a date
 * Same date will always produce the same seed
 */
export function getWeekSeed(weekStartDate: Date): number {
  const year = weekStartDate.getFullYear();
  const month = weekStartDate.getMonth();
  const date = weekStartDate.getDate();
  
  // Create a consistent seed based on the week start date
  return year * 10000 + month * 100 + date;
}

/**
 * Shuffle an array using a seeded random number generator
 * Same seed will always produce the same shuffle order
 */
export function seededShuffle<T>(array: T[], seed: number): T[] {
  const shuffled = [...array];
  const rng = new SeededRandom(seed);
  
  // Fisher-Yates shuffle with seeded random
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(rng.next() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j]!, shuffled[i]!];
  }
  
  return shuffled;
}

/**
 * Randomize recipes based on week start date
 * Same week will always produce the same order
 */
export function randomizeRecipesForWeek<T>(recipes: T[], weekStartDate: Date | undefined): T[] {
  if (!weekStartDate) {
    // If no week date provided, return original order
    return recipes;
  }
  
  const seed = getWeekSeed(weekStartDate);
  return seededShuffle(recipes, seed);
}
