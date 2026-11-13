// Shared text utilities retained for the existing Recommendation lane.
export function normalizeText(value: string): string {
  return (value || "")
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/\btainghe\b/g, "tai nghe")
    .replace(/\bbanphim\b/g, "ban phim")
    .replace(/[^a-z0-9\s]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

export const SYNONYM_MAP: Record<string, string[]> = {
  headphone: ["tai nghe", "tai nghe khong day", "headphone", "headphones", "earphone", "earphones", "chong on", "noise cancelling", "noise cancellation", "nghe nhac"],
  keyboard: ["ban phim", "keyboard", "ban phim co", "mechanical keyboard", "switch", "go phim", "keycap", "rgb"],
  watch: ["dong ho", "smart watch", "smartwatch", "dong ho thong minh", "the thao", "suc khoe", "dem buoc"],
  mouse: ["chuot", "mouse", "chuot gaming", "chuot khong day", "chuot may tinh"],
  computer: ["laptop", "may tinh", "pc", "macbook", "may tinh ban", "man hinh"],
  cleanCode: ["clean code", "robert martin", "uncle bob", "refactor"],
  pragmaticProgrammer: ["pragmatic programmer", "lap trinh thuc dung", "david thomas", "andrew hunt"],
  book: ["sach", "book", "sach lap trinh", "sach code", "giao trinh", "sach ky thuat"],
  tshirt: ["ao thun", "t shirt", "tshirt", "ao phong", "cotton", "ao ngan tay"],
  jacket: ["ao khoac", "jacket", "denim", "khoac jean", "ao am"],
  clothing: ["thoi trang", "quan ao", "outfit", "quan jean", "vay", "dam", "ao so mi", "hoodie", "ao len", "giay", "dep"],
};
