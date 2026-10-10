# 归零现场 · Requiem 视觉设计

程序名：**归零现场 · Requiem**。以《JOJO》中黄金体验镇魂曲为灵感，把“归零”转译为找回工作中断前的现场。

Logo 使用金色甲胄 / 甲虫轮廓、紫色核心和逆向回环箭头。透明背景，无文字；深紫色描边使轮廓在浅色、深色界面均可辨认。挂件采用深紫底色和金色数字，窗口使用淡紫背景。

- `requiem-logo.png`：内置 ImageGen 生成的透明原图。
- `requiem.ico`：16、24、32、48、64、128、256 像素 Windows 图标，嵌入 EXE 和托盘。
- `requiem-widget.png`：32 像素派生资源，供挂件和面板加载。
- `build-icons.ps1`：用 Windows System.Drawing 进行尺寸转换和 ICO 打包；不重新绘制或修改生成图的设计。
- `widget-preview.png`：挂件渲染预览。

生成方式：内置 `image_gen`，透明背景；程序运行时不需要图像生成工具、网络或云端 API。

## 最终生成提示词

```text
Use case: logo-brand.
Asset type: a single original software application logo for a lightweight Windows desktop widget called "归零现场 · REQUIEM".
Primary request: A JoJo's Bizarre Adventure Gold Experience Requiem inspired emblem translating its golden armor, purple accents, arrow and return-to-zero idea into a clean original software icon. A stylized gold beetle / golden armored crest with a purple diamond-like forehead inset, combined with ONE bold counterclockwise circular return arrow forming a broken halo behind the crest. The central emblem subtly resembles a zero with an angular armored mask. It should feel elegant, uncanny, powerful and precise rather than cartoonish.
Composition: square icon, single centered emblem occupying 80% of canvas, symmetric frontal silhouette, strong readable outer contour. No typography. Transparent outer background.
Style: polished flat vector-like emblem with 2-3 broad gold tone planes, restrained highlights and thick very dark purple outlines. Deep plum and luminous amber-gold palette, small lilac accent. Large simple shapes readable at 16px, 32px and 48px, limited detail, generous negative spaces.
Constraints: one icon only, no text, no lettering, no watermark, no mockup, no surrounding collage, no gradients spreading outside the mark, no photorealism, no thin ornamental filigree, no character portrait or full body. Actual transparent alpha background.
```
