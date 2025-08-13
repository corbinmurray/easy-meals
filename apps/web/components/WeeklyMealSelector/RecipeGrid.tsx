"use client";

import RecipeCard from "@/components/RecipeCard";
import { randomizeRecipesForWeek } from "@/lib/randomization";
import type { Recipe } from "@/types/meal-planner";
import { Badge } from "@workspace/ui/components/badge";
import { Input } from "@workspace/ui/components/input";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@workspace/ui/components/pagination";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@workspace/ui/components/select";
import {
  ChevronDown,
  ChevronUp,
  MoreHorizontal,
  Search,
  X,
} from "lucide-react";
import { AnimatePresence, motion } from "motion/react";
import { useEffect, useMemo, useState } from "react";

interface RecipeGridProps {
  recipes: Recipe[];
  isRecipeSelected: (recipeId: string) => boolean;
  onRecipeSelect: (recipeId: string) => void;
  maxSelectionsReached: boolean;
  // New props for enhanced functionality
  currentWeekStart?: Date; // For week-based randomization
  recipesPerPage?: number; // For pagination (default: 12)
  enableSearch?: boolean; // Enable/disable search (default: true)
  enableTagFiltering?: boolean; // Enable/disable tag filtering (default: true)
  enablePagination?: boolean; // Enable/disable pagination (default: true)
}

export default function RecipeGrid({
  recipes,
  isRecipeSelected,
  onRecipeSelect,
  maxSelectionsReached,
  currentWeekStart,
  recipesPerPage = 12,
  enableSearch = true,
  enableTagFiltering = true,
  enablePagination = true,
}: RecipeGridProps) {
  // State for search functionality
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState("");

  // State for tag filtering
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [showAllTags, setShowAllTags] = useState(false);
  const [tagSearchTerm, setTagSearchTerm] = useState("");

  // State for pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(recipesPerPage);

  // Debounce search term
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearchTerm(searchTerm);
    }, 300);

    return () => clearTimeout(timer);
  }, [searchTerm]);

  // Reset page when search or filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [debouncedSearchTerm, selectedTags]);

  // Reset page when page size changes
  useEffect(() => {
    setCurrentPage(1);
  }, [pageSize]);

  // Reset show all tags when tag search changes
  useEffect(() => {
    setShowAllTags(false);
  }, [tagSearchTerm]);

  // Extract unique tags with frequency from all recipes
  const tagData = useMemo(() => {
    const tagCounts = new Map<string, number>();
    recipes.forEach((recipe) => {
      recipe.tags?.forEach((tag) => {
        tagCounts.set(tag, (tagCounts.get(tag) || 0) + 1);
      });
    });

    // Convert to array and sort by frequency (most popular first)
    return Array.from(tagCounts.entries())
      .map(([tag, count]) => ({ tag, count }))
      .sort((a, b) => b.count - a.count);
  }, [recipes]);

  // Filter tags based on search term
  const filteredTagData = useMemo(() => {
    if (!tagSearchTerm.trim()) return tagData;

    const searchLower = tagSearchTerm.toLowerCase();
    return tagData.filter(({ tag }) => tag.toLowerCase().includes(searchLower));
  }, [tagData, tagSearchTerm]);

  // Determine which tags to display
  const displayedTags = useMemo(() => {
    const tagsToShow = showAllTags
      ? filteredTagData
      : filteredTagData.slice(0, 8);
    return tagsToShow;
  }, [filteredTagData, showAllTags]);

  // Search and filter logic
  const filteredRecipes = useMemo(() => {
    let filtered = [...recipes];

    // Apply search filter
    if (enableSearch && debouncedSearchTerm.trim()) {
      const searchLower = debouncedSearchTerm.toLowerCase();
      filtered = filtered.filter((recipe) => {
        // Search in name
        if (recipe.name.toLowerCase().includes(searchLower)) return true;

        // Search in description
        if (recipe.description.toLowerCase().includes(searchLower)) return true;

        // Search in tags
        if (recipe.tags?.some((tag) => tag.toLowerCase().includes(searchLower)))
          return true;

        return false;
      });
    }

    // Apply tag filtering (AND logic - all selected tags must be present)
    if (enableTagFiltering && selectedTags.length > 0) {
      filtered = filtered.filter((recipe) => {
        return selectedTags.every((selectedTag) =>
          recipe.tags?.includes(selectedTag)
        );
      });
    }

    // Apply week-based randomization
    filtered = randomizeRecipesForWeek(filtered, currentWeekStart);

    return filtered;
  }, [
    recipes,
    debouncedSearchTerm,
    selectedTags,
    enableSearch,
    enableTagFiltering,
    currentWeekStart,
  ]);

  // Pagination logic
  const totalPages = enablePagination
    ? Math.ceil(filteredRecipes.length / pageSize)
    : 1;
  const paginatedRecipes = enablePagination
    ? filteredRecipes.slice(
        (currentPage - 1) * pageSize,
        currentPage * pageSize
      )
    : filteredRecipes;

  // Ensure current page is valid when filters change
  useEffect(() => {
    if (currentPage > totalPages && totalPages > 0) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  // Generate pagination items with ellipsis for large page counts
  const generatePaginationItems = (): (number | string)[] => {
    const items: (number | string)[] = [];
    const maxVisiblePages = 7; // Show up to 7 page numbers + ellipsis

    if (totalPages <= maxVisiblePages) {
      // Show all pages if we have few pages
      for (let i = 1; i <= totalPages; i++) {
        items.push(i);
      }
    } else {
      // Smart pagination with ellipsis
      const startPages = [1];
      const endPages = [totalPages];
      const aroundCurrent = [];

      // Add pages around current page
      const start = Math.max(2, currentPage - 1);
      const end = Math.min(totalPages - 1, currentPage + 1);

      for (let i = start; i <= end; i++) {
        aroundCurrent.push(i);
      }

      // Combine and add ellipsis where needed
      if (aroundCurrent.length > 0 && aroundCurrent[0]! > 2) {
        items.push(...startPages, "...", ...aroundCurrent);
      } else {
        items.push(...startPages, ...aroundCurrent);
      }

      if (
        aroundCurrent.length > 0 &&
        aroundCurrent[aroundCurrent.length - 1]! < totalPages - 1
      ) {
        items.push("...", ...endPages);
      } else {
        items.push(...endPages);
      }

      // Remove duplicates
      const uniqueItems: (number | string)[] = [];
      for (const item of items) {
        if (!uniqueItems.includes(item)) {
          uniqueItems.push(item);
        }
      }
      items.splice(0, items.length, ...uniqueItems);
    }

    return items;
  };
  return (
    <motion.section
      className="w-full"
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5, delay: 0.2 }}
    >
      <div className="mb-6">
        <h3 className="text-xl font-semibold mb-2">Available Recipes</h3>
        <p className="text-muted-foreground">
          {maxSelectionsReached
            ? "You've reached the maximum of 7 recipes for this week. Remove some to select others."
            : ""}
        </p>
      </div>

      {/* Search and Filter Controls */}
      <div className="mb-6 space-y-8">
        {/* Search Input */}
        {enableSearch && (
          <motion.div
            className="relative"
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3 }}
          >
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-muted-foreground h-4 w-4" />
            <Input
              type="text"
              placeholder="Search recipes by name, description, or tags..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="pl-10 w-full"
            />
          </motion.div>
        )}

        {/* Tag Filtering */}
        {enableTagFiltering && tagData.length > 0 && (
          <motion.div
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3, delay: 0.1 }}
          >
            <div className="flex items-center justify-between mb-3">
              <h4 className="text-sm font-medium">Filter by Tags</h4>
              {selectedTags.length > 0 && (
                <motion.button
                  onClick={() => setSelectedTags([])}
                  className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1"
                  whileHover={{ scale: 1.05 }}
                  whileTap={{ scale: 0.95 }}
                  initial={{ opacity: 0, x: 10 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: 10 }}
                >
                  <X className="size-3" />
                  Clear all
                </motion.button>
              )}
            </div>

            {/* Available tags */}
            <div className="space-y-2">
              <div className="flex flex-wrap gap-2">
                {displayedTags.map(({ tag, count }) => {
                  const isSelected = selectedTags.includes(tag);
                  return (
                    <motion.button
                      key={tag}
                      whileHover={{ scale: 1.02 }}
                      whileTap={{ scale: 0.98 }}
                      onClick={() => {
                        setSelectedTags((prev) =>
                          isSelected
                            ? prev.filter((t) => t !== tag)
                            : [...prev, tag]
                        );
                      }}
                      className="group"
                    >
                      <Badge
                        variant={isSelected ? "default" : "secondary"}
                        className={`cursor-pointer transition-all duration-200 text-xs ${
                          isSelected
                            ? "bg-primary text-primary-foreground shadow-sm"
                            : "hover:bg-secondary/80"
                        }`}
                      >
                        {tag}
                        <span className="ml-1 text-xs opacity-70">
                          ({count})
                        </span>
                      </Badge>
                    </motion.button>
                  );
                })}
              </div>

              {/* Show more/less button */}
              {filteredTagData.length > 8 && (
                <motion.button
                  onClick={() => setShowAllTags(!showAllTags)}
                  className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1 mt-2"
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                >
                  {showAllTags ? (
                    <>
                      <ChevronUp className="h-3 w-3" />
                      Show less tags
                    </>
                  ) : (
                    <>
                      <ChevronDown className="h-3 w-3" />
                      Show {filteredTagData.length - 8} more tags
                    </>
                  )}
                </motion.button>
              )}

              {/* No tags found message */}
              {tagSearchTerm && filteredTagData.length === 0 && (
                <motion.p
                  className="text-xs text-muted-foreground"
                  initial={{ opacity: 0, y: -10 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ duration: 0.2 }}
                >
                  No tags found matching "{tagSearchTerm}"
                </motion.p>
              )}
            </div>
          </motion.div>
        )}
      </div>

      <AnimatePresence mode="wait">
        <motion.div
          key={`${debouncedSearchTerm}-${selectedTags.join(",")}-${currentPage}`}
          className="grid gap-6 grid-cols-[repeat(auto-fit,minmax(340px,1fr))] justify-center"
          layout
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.2 }}
        >
          {paginatedRecipes.map((recipe, index) => {
            const selected = isRecipeSelected(recipe.id);
            const canSelect = selected || !maxSelectionsReached;

            return (
              <motion.div
                key={recipe.id}
                layout
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{
                  duration: 0.4,
                  delay: index * 0.03, // Slightly faster stagger for better responsiveness
                  type: "spring",
                  stiffness: 300,
                  damping: 30,
                }}
                className={`${!canSelect ? "opacity-60 pointer-events-none" : ""}`}
              >
                <RecipeCard
                  recipe={recipe}
                  selected={selected}
                  onSelect={() => {
                    if (canSelect) {
                      onRecipeSelect(recipe.id);
                    }
                  }}
                />
              </motion.div>
            );
          })}
        </motion.div>
      </AnimatePresence>

      {/* No results message */}
      {filteredRecipes.length === 0 && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          className="text-center py-12"
        >
          <p className="text-muted-foreground text-lg">
            No recipes found matching your search criteria.
          </p>
          {(debouncedSearchTerm || selectedTags.length > 0) && (
            <p className="text-sm text-muted-foreground mt-2">
              Try adjusting your search terms or clearing filters.
            </p>
          )}
        </motion.div>
      )}

      {/* Pagination */}
      {enablePagination && totalPages > 1 && filteredRecipes.length > 0 && (
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.3 }}
          className="mt-8 space-y-4"
        >
          {/* Results info and page size selector */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div className="text-sm text-muted-foreground text-center sm:text-left">
              Showing {(currentPage - 1) * pageSize + 1}-
              {Math.min(currentPage * pageSize, filteredRecipes.length)} of{" "}
              {filteredRecipes.length} recipes
            </div>

            <div className="flex items-center gap-2 justify-center sm:justify-end">
              <span className="text-sm text-muted-foreground">Show:</span>
              <Select
                value={pageSize.toString()}
                onValueChange={(value) => setPageSize(Number(value))}
              >
                <SelectTrigger className="w-20">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="6">6</SelectItem>
                  <SelectItem value="12">12</SelectItem>
                  <SelectItem value="24">24</SelectItem>
                  <SelectItem value="48">48</SelectItem>
                </SelectContent>
              </Select>
              <span className="text-sm text-muted-foreground">per page</span>
            </div>
          </div>

          {/* Pagination controls */}
          <div className="flex justify-center">
            <Pagination>
              <PaginationContent>
                <PaginationItem>
                  <PaginationPrevious
                    onClick={() =>
                      setCurrentPage((prev) => Math.max(1, prev - 1))
                    }
                    className={
                      currentPage === 1
                        ? "pointer-events-none opacity-50"
                        : "cursor-pointer"
                    }
                  />
                </PaginationItem>

                {generatePaginationItems().map((item, index) => (
                  <PaginationItem key={index}>
                    {typeof item === "number" ? (
                      <PaginationLink
                        onClick={() => setCurrentPage(item)}
                        isActive={currentPage === item}
                        className="cursor-pointer"
                      >
                        {item}
                      </PaginationLink>
                    ) : (
                      <span className="flex h-9 w-9 items-center justify-center text-sm text-muted-foreground">
                        <MoreHorizontal className="h-4 w-4" />
                      </span>
                    )}
                  </PaginationItem>
                ))}

                <PaginationItem>
                  <PaginationNext
                    onClick={() =>
                      setCurrentPage((prev) => Math.min(totalPages, prev + 1))
                    }
                    className={
                      currentPage === totalPages
                        ? "pointer-events-none opacity-50"
                        : "cursor-pointer"
                    }
                  />
                </PaginationItem>
              </PaginationContent>
            </Pagination>
          </div>
        </motion.div>
      )}

      {maxSelectionsReached && (
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          className="mt-6 p-4 bg-warning/10 border border-warning/20 rounded-lg text-center"
        >
          <p className="text-sm text-warning-foreground">
            You've selected the maximum of 7 recipes for this week. To select a
            different recipe, please remove one from your current selection.
          </p>
        </motion.div>
      )}
    </motion.section>
  );
}
