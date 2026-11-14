import { useBrands, useCatalogAttributes, type ProductInput } from "../api/hooks";
import type { ProductAttributeInput } from "../types";
import { Input, Select } from "./ui";

type Props = {
  categoryId: number;
  value: Pick<ProductInput, "sku" | "brandId" | "clearBrand" | "attributes">;
  onChange: (patch: Partial<ProductInput>) => void;
};

export function CatalogProductFields({ categoryId, value, onChange }: Props) {
  const { data: brands } = useBrands();
  const { data: definitions } = useCatalogAttributes(categoryId);
  const attributes = value.attributes ?? [];

  const updateAttribute = (definitionId: number, raw: string, dataType: 0 | 1 | 2) => {
    const remaining = attributes.filter((x) => x.attributeDefinitionId !== definitionId);
    if (raw === "") {
      onChange({ attributes: remaining });
      return;
    }

    let next: ProductAttributeInput;
    if (dataType === 0) {
      next = { attributeDefinitionId: definitionId, valueText: raw };
    } else if (dataType === 1) {
      const number = Number(raw);
      if (!Number.isFinite(number)) return;
      next = { attributeDefinitionId: definitionId, valueNumber: number };
    } else {
      next = { attributeDefinitionId: definitionId, valueBoolean: raw === "true" };
    }
    onChange({ attributes: [...remaining, next] });
  };

  return (
    <div className="space-y-2 rounded-xl border border-slate-200 dark:border-slate-800 p-3">
      <div>
        <label className="block text-xs font-medium muted mb-1">SKU</label>
        <Input value={value.sku ?? ""} maxLength={100}
          placeholder="Để trống để hệ thống tự tạo SKU"
          onChange={(e) => onChange({ sku: e.target.value })} />
      </div>

      <div>
        <label className="block text-xs font-medium muted mb-1">Thương hiệu</label>
        <Select
          value={value.clearBrand ? "" : value.brandId ?? ""}
          onChange={(e) => {
            const selected = e.target.value;
            onChange(selected
              ? { brandId: Number(selected), clearBrand: false }
              : { brandId: undefined, clearBrand: !!value.brandId });
          }}
        >
          <option value="">Không chọn thương hiệu</option>
          {brands?.map((brand) => <option key={brand.id} value={brand.id}>{brand.name}</option>)}
        </Select>
      </div>

      <div>
        <div className="text-xs font-semibold mb-2">Thuộc tính kỹ thuật</div>
        {categoryId <= 0 ? (
          <p className="muted text-xs">Chọn danh mục để tải các thuộc tính tương ứng.</p>
        ) : definitions && definitions.length === 0 ? (
          <p className="muted text-xs">Danh mục này chưa có thuộc tính. Admin có thể khai báo ở phần Quản lý catalog.</p>
        ) : (
          <div className="space-y-2">
            {definitions?.map((definition) => {
              const current = attributes.find((x) => x.attributeDefinitionId === definition.id);
              const label = definition.displayName
                + (definition.unit ? " (" + definition.unit + ")" : "")
                + (definition.isRequired ? " *" : "");
              if (definition.dataType === 0) {
                return (
                  <div key={definition.id}>
                    <label className="block text-xs muted mb-1">{label}</label>
                    <Input maxLength={2000} value={current?.valueText ?? ""} placeholder="Nhập giá trị"
                      onChange={(e) => updateAttribute(definition.id, e.target.value, 0)} />
                  </div>
                );
              }
              if (definition.dataType === 1) {
                return (
                  <div key={definition.id}>
                    <label className="block text-xs muted mb-1">{label}</label>
                    <Input type="number" step="0.0001" value={current?.valueNumber ?? ""} placeholder="Nhập giá trị số"
                      onChange={(e) => updateAttribute(definition.id, e.target.value, 1)} />
                  </div>
                );
              }
              return (
                <div key={definition.id}>
                  <label className="block text-xs muted mb-1">{label}</label>
                  <Select
                    value={current?.valueBoolean === undefined ? "" : current.valueBoolean ? "true" : "false"}
                    onChange={(e) => updateAttribute(definition.id, e.target.value, 2)}
                  >
                    <option value="">Chưa chọn</option>
                    <option value="true">Có</option>
                    <option value="false">Không</option>
                  </Select>
                </div>
              );
            })}
          </div>
        )}
        <p className="muted text-[11px] mt-2">Các trường có dấu * là bắt buộc. Kiểu dữ liệu được Admin xác định theo danh mục.</p>
      </div>
    </div>
  );
}
