"""Build four source illustrations for every catalog entry, including atlas provenance.
Run with Python + PyMuPDF + Pillow after fetching the four UMN heart JPGs.
"""
from pathlib import Path
import hashlib,json,shutil,zipfile
from collections import Counter
import pymupdf
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'output/element-atlas-full-upload'
PREFIX='https://zgmwmaustzdsqybamalu.supabase.co/storage/v1/object/public/element-content/images/'
CATALOGS=[ROOT/p for p in ('Assets/Content/elements.example.json','Assets/Resources/ElementContent/elements.json','Documentation/RemoteContent/elements.example.json')]
SOURCES=json.loads((ROOT/'output/atlas-images/sources.json').read_text())
SOURCE_MAP={s['id']:s for s in SOURCES}
ATLAS='01_Popesko_older_edition_592pages'
BRAIN='12_Canine_Brain_MRI_STATIC_EXPORT'
HEAD='13_Canine_Head_MRI_STATIC_EXPORT'

def P(n):return ('popesko',n)
def B(n):return ('brain',n)
def H(n):return ('head',n)
def W(n):return ('heart',n)

SELECTION={
 'heart':[W(10),W(9),W(11),W(12)],
 'brain':[P(183),P(184),B(2),B(10)],
 'eye':[H(1),H(3),P(171),P(176)],
 'dog_frontal_bone':[P(173),P(172),P(174),P(175)],
 'dog_frontalis':[P(177),P(178),P(181),P(183)],
 'dog_incisive_bone':[P(173),P(172),P(175),P(176)],
 'dog_levator_nasolabialis':[P(177),P(178),P(180),P(185)],
 'dog_levator_nasolabialis_cut':[P(177),P(178),P(180),P(185)],
 'dog_mandible':[P(172),P(173),P(174),P(176)],
 'dog_masseter':[P(178),P(179),P(181),P(182)],
 'dog_levator_anguli_oculi_medialis':[P(177),P(178),P(181),H(1)],
 'dog_nasal_bone':[P(173),P(172),P(174),P(175)],
 'dog_occipital_bone':[P(173),P(172),P(174),P(176)],
 'dog_occipital_bone_2':[P(173),P(172),P(174),P(176)],
 'dog_orbicularis_oculi':[P(177),P(178),P(181),H(1)],
 'dog_parietal_bone':[P(173),P(172),P(174),P(175)],
 'dog_teeth':[P(172),P(174),P(176),P(180)],
 'dog_teeth_2':[P(172),P(174),P(176),P(180)],
 'dog_temporalis':[P(178),P(179),P(181),P(182)],
 'dog_malaris':[P(177),P(178),P(181),H(3)],
 'dog_zygomatic_temporal_process':[P(172),P(173),P(176),P(179)],
 'dog_temporal_zygomatic_process':[P(172),P(173),P(176),P(179)],
 'dog_maxilla':[P(173),P(172),P(175),P(176)],
 'dog_zygomaticus':[P(177),P(178),P(179),P(185)],
 'dog_pons':[B(9),B(10),B(11),P(184)],
 'dog_cerebellum':[B(9),B(10),B(11),B(12)],
 'dog_diencephalon':[B(4),B(5),B(6),P(183)],
 'dog_medulla':[B(12),B(13),B(14),B(15)],
 'dog_midbrain':[B(6),B(7),B(8),B(9)],
 'dog_olfactory_bulbs':[P(184),B(1),B(17),H(9)],
 'dog_left_hemisphere':[B(2),B(3),B(4),B(5)],
 'dog_right_hemisphere':[B(2),B(3),B(4),B(5)],
 'dog_right_eye':[H(1),H(3),P(171),P(176)],
 'dog_left_eye':[H(1),H(3),P(171),P(176)],
 'dog_head_skin':[P(171),P(177),P(181),P(182)],
 'dog_head_fur':[P(171),P(177),H(1),H(3)],
 'dog_skin_inner':[P(171),P(180),P(181),P(182)]
}
TITLES={171:'Области головы собаки',172:'Череп собаки — вид слева',173:'Кости черепа собаки — цветовая схема',174:'Череп собаки — сагиттальный разрез',175:'Череп собаки — вид сверху',176:'Череп собаки — вид снизу',177:'Поверхностные мышцы головы собаки',178:'Мышцы и сосуды головы собаки',179:'Глубокие мышцы головы собаки',180:'Сагиттальный разрез головы собаки',181:'Срез головы в плоскости мышечного отростка',182:'Срез головы в плоскости челюстного сустава',183:'Головной мозг собаки — обзор',184:'Головной мозг собаки — вентральный вид',185:'Сосуды головы собаки'}
HEART_TITLES={9:'Вскрытое правое предсердие',10:'Сердце — краниальный вид',11:'Вскрытые правые отделы сердца',12:'Левые отделы сердца и клапаны'}
HEART_CAPTIONS={9:'Анатомический препарат: полость правого предсердия и прилежащие структуры.',10:'Изолированный препарат сердца: внешний вид со стороны крупных сосудов.',11:'Изолированный препарат: вскрытые правый желудочек и лёгочный ствол.',12:'Изолированный препарат: разрез левого предсердия и желудочка, клапанный аппарат.'}

def write(path,value):
 path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')

def main():
 OUT.mkdir(exist_ok=True,parents=True);(OUT/'images').mkdir(exist_ok=True)
 catalog=json.loads(CATALOGS[0].read_text());assert set(SELECTION)=={e['id'] for e in catalog['elements']}
 originals=json.loads((ROOT/'output/atlas-images/image_catalog.json').read_text())
 documents={};assets={};provenance=[]
 for kind,page in sorted({p for v in SELECTION.values() for p in v}):
  name=f'atlas-v11-{kind}-{page:03d}.jpg' if kind!='heart' else f'umn-heart-{page}.jpg'
  dest=OUT/'images'/name
  if kind=='heart':
   assert dest.is_file(),dest
   url=f'https://vanat.ahc.umn.edu/carnLabs/Lab12/Img12-{page}.html'
   title=HEART_TITLES[page];caption=HEART_CAPTIONS[page]
   provenance.append({'file':name,'kind':'internet-dissection-photo','source_url':url,'image_url':f'https://vanat.ahc.umn.edu/carnLabs/Lab12/Images/Large12-{page}.jpg','publisher':'University of Minnesota, Carnivore Anatomy, Lab 12','species_note':'Individual image page does not specify the specimen species.'})
  else:
   sid={'popesko':ATLAS,'brain':BRAIN,'head':HEAD}[kind]
   source=SOURCE_MAP[sid]
   if kind=='popesko':
    candidates=[e for e in originals if any(loc['source']==sid and loc['page']==page for loc in e['locations'])]
    entry=max(candidates,key=lambda e:e['width']*e['height']);shutil.copyfile(ROOT/'output/atlas-images/images'/entry['file'],dest)
    title=TITLES[page];caption='Таблица атласа Попеско, PDF-страница '+str(page)+'. Подписи и условные обозначения сохранены.'
   else:
    if sid not in documents:documents[sid]=pymupdf.open(source['pdf'])
    p=documents[sid][page-1];scale=2048/max(p.rect.width,p.rect.height)
    p.get_pixmap(matrix=pymupdf.Matrix(scale,scale),alpha=False).save(str(dest),jpg_quality=91)
    if kind=='brain':
     level=(page-1)%16+1;sequence='T1' if page<=16 else 'T2'
     title=f'Мозг собаки — уровень {level:02d}, {sequence}'
     caption=f'МРТ и цветной срез с обозначениями. Canine Brain MRI & Brain Tissue Atlas, PDF-страница {page}.'
    else:
     title=f'Голова собаки — МРТ, уровень {((page-1)//2+1):02d}'
     caption=f'Структуры головы собаки на поперечном МРТ-срезе с обозначениями. Canine Head MRI Atlas, PDF-страница {page}.'
   source_url={'brain':'https://vanat.ahc.umn.edu/mriBrainAtlas/','head':'https://vanat.ahc.umn.edu/mriHeadAtlas/'} .get(kind)
   provenance.append({'file':name,'kind':'local-atlas','source_id':sid,'pdf':source['pdf'],'pdf_page':page,'source_url':source_url,'presentation':'Original annotated plate; no synthetic illustration.'})
  assets[(kind,page)]={'title':title,'caption':caption,'url':PREFIX+name}

 mapping=[]
 for element in catalog['elements']:
  key=element['id'];images=[]
  for ref in SELECTION[key]:
   image=dict(assets[ref])
   if key in ('dog_left_hemisphere','dog_right_hemisphere','dog_left_eye','dog_right_eye'):
    image['caption']+=' Обзор включает обе стороны; выбранная сторона отдельно не выделена.'
   if key in ('dog_occipital_bone','dog_occipital_bone_2','dog_levator_nasolabialis_cut','dog_teeth','dog_teeth_2'):
    image['caption']+=' Границы отдельного фрагмента или группы из модели на таблице не отмечены.'
   if key=='dog_head_fur':
    image['caption']+=' Обзор головы и подлежащих тканей; микроструктура шерсти отдельно не показана.'
   if key=='dog_head_skin' and ref!=P(171):
    image['caption']+=' Показаны подлежащие ткани и анатомические ориентиры головы.'
   if key=='dog_skin_inner':
    image['caption']+=' Только ориентиры головы: технический фрагмент Skin.fbx не идентифицирован как самостоятельная анатомическая структура.'
   if key in ('dog_malaris','dog_levator_anguli_oculi_medialis'):
    image['caption']+=' Используется для ориентации в области век; выбранная мышца модели отдельно не выделена.'
   images.append(image)
  element['images']=images
  element.pop('imageUrls',None)
  if key=='brain':
   icon='brain-icon-photo.jpg';shutil.copyfile(ROOT/'output/atlas-top10/images'/icon,OUT/'images'/icon);element['iconUrl']=PREFIX+icon
  else:element['iconUrl']=images[0]['url']
  if key=='heart':
   element['shortDescription']='Мышечный орган, обеспечивающий движение крови по сосудам.'
   element['fullDescription']='Сердце состоит из предсердий и желудочков. Галерея содержит анатомические препараты из учебного курса по анатомии плотоядных; вид животного на страницах отдельных снимков не уточнён.'
  mapping.append({'id':key,'name':element['name'],'imageCount':len(images),'images':images})
 catalog['version']=11
 # Verify all images and pairwise byte/pixel uniqueness within each gallery.
 pixels={};files={}
 for element in catalog['elements']:
  assert len(element['images'])==4
  urls=[i['url'] for i in element['images']];assert len(set(urls))==4
  for url in urls+[element['iconUrl']]:
   path=OUT/'images'/url.rsplit('/',1)[-1];assert path.is_file(),path
   if path.name not in pixels:
    with Image.open(path) as im:
     im.load();assert im.width>=300 and im.height>=177
     pixels[path.name]=hashlib.sha256(im.convert('RGB').tobytes()).hexdigest();files[path.name]=hashlib.sha256(path.read_bytes()).hexdigest()
  assert len({pixels[u.rsplit('/',1)[-1]] for u in urls})==4,element['id']
 provenance.append({'file':'brain-icon-photo.jpg','kind':'atlas-photo-icon','source':json.loads((ROOT/'output/atlas-top10/sources.json').read_text())[-1]})
 write(OUT/'data/elements.example.json',catalog);write(OUT/'element-images.json',mapping);write(OUT/'sources.json',provenance)
 write(OUT/'checksums.json',files)
 # Contact sheets for every final image, named by page and matching the source manifest.
 names=sorted(files);font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',17)
 for start in range(0,len(names),24):
  chunk=names[start:start+24];sheet=Image.new('RGB',(1440,((len(chunk)+5)//6)*275),'#edf1f2');draw=ImageDraw.Draw(sheet)
  for n,name in enumerate(chunk):
   im=Image.open(OUT/'images'/name);im.thumbnail((230,240));x=(n%6)*240;y=(n//6)*275;sheet.paste(im,(x+(240-im.width)//2,y));draw.text((x+3,y+244),name.replace('atlas-v11-',''),font=font,fill='#18323c')
  (OUT/'previews').mkdir(exist_ok=True);sheet.save(OUT/'previews'/f'selected-{start//24+1}.jpg',quality=90)
 (OUT/'README.txt').write_text('''ПОЛНЫЙ КАТАЛОГ: 37 записей, по 4 разные иллюстрации в каждой, JSON v11.

Загрузка в Supabase, bucket element-content:
1. Все файлы из images/ загрузить в папку images/ бакета, без дополнительной вложенности.
2. Заменить data/elements.example.json файлом из папки data/ архива.
3. Полностью перезапустить приложение с интернетом.

Сначала картинки, затем JSON. Старые картинки можно оставить: новый каталог использует только ссылки из этого архива.

Галереи содержат иллюстрации из атласов и четыре учебные фотографии сердца UMN. Виды 3D-моделей в галереи не включены. Для разных элементов одной области одна таблица может повторяться; внутри галереи одного элемента все четыре изображения различны. Парные части с общим ID используют одну галерею. 34 анатомических ID головы соответствуют 43 деталям сцены; ещё 3 записи — brain, eye и heart.

Обзорные изображения, обе стороны головы и неидентифицированный технический фрагмент подписаны честно, без выдуманного выделения структуры. Источники и страницы — sources.json; соответствия элементов — element-images.json; previews/ — контактные листы. Эти вспомогательные файлы загружать в бакет не нужно. Единственная дополнительная иконка brain-icon-photo.jpg — ранее выбранная фотография препарата мозга из локального атласа.
''')
 write(OUT/'validation.json',{'catalogVersion':11,'elements':len(catalog['elements']),'galleryReferences':sum(len(e['images']) for e in catalog['elements']),'galleryImagesPerElement':4,'uniqueGalleryImages':len(assets),'totalImageFilesIncludingIcons':len(files),'allFilesVerified':True,'distinctPixelsWithinEachGallery':True})
 # Keep catalogs in sync only after all assets validate.
 for path in CATALOGS:write(path,catalog)
 archive=ROOT/'output/element-atlas-full-upload.zip'
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
  for path in sorted(OUT.rglob('*')):
   if path.is_file() and not path.name.startswith('.'):z.write(path,path.relative_to(OUT))
 with zipfile.ZipFile(archive) as z:assert z.testzip() is None
 print((OUT/'validation.json').read_text());print('Archive bytes:',archive.stat().st_size)

if __name__=='__main__':main()
