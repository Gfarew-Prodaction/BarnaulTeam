"""Package verified existing atlas plates and downloaded UMN teaching photographs."""
from pathlib import Path
import json,hashlib,shutil,re,zipfile
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'output/final-element-content';PREFIX='https://zgmwmaustzdsqybamalu.supabase.co/storage/v1/object/public/element-content/images/'
def write(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
cat=json.loads((ROOT/'Assets/Content/elements.example.json').read_text());cat['version']=13
web=json.loads((OUT/'build/web-image-urls.json').read_text())
titles={
'22-1':('Поверхностные мышцы лица','Фото препарата: мышцы мимики, область век, носа и губ.'),
'22-2':('Мышцы губ и щеки','Фото препарата с обзором носогубной и окологлазничной области.'),
'22-3':('Голова — дорсальный вид','Фото препарата: мышцы области ушной раковины и височной области.'),
'22-4':('Веки и глазная щель','Фото препарата: веки, третье веко и видимая поверхность глазного яблока.'),
'22-5':('Ротовая полость и зубной ряд','Фото рассечённой головы: ротовая полость, зубной ряд и нёбо. Вид животного на странице не уточнён.'),
'22-9':('Голова собаки — срединный разрез','Фото препарата собаки: полость черепа, мозг, носовая перегородка и нёбо.'),
'22-12':('Носовая полость и лобная пазуха','Фото препарата: носовая перегородка удалена, видны носовые раковины и область лобной пазухи.'),
'23-3':('Челюсть, скуловая дуга и жевательные мышцы','Фото препарата: боковой вид нижней челюсти, скуловой дуги и жевательных мышц.'),
'23-4':('Скуловая дуга и жевательный аппарат','Фото препарата: взаимное положение скуловой дуги, височной и жевательной мышц, крупных щёчных зубов.'),
'23-6':('Ветвь нижней челюсти и сустав','Фото препарата после удаления скуловой дуги; видны ветвь нижней челюсти и область сустава.'),
'23-10':('Голова собаки — поперечный срез','Фото поперечного среза головы собаки: свод черепа, челюсть, скуловая дуга и жевательные мышцы.'),
'24-1':('Глазница и окружающие структуры','Фото препарата: область глазницы после удаления периорбиты, наружные мышцы глаза и слёзная железа.')}
extras={}
for e in cat['elements']:
 id=e['id']
 if id=='heart':keys=[]
 elif id in ['brain','dog_left_hemisphere','dog_right_hemisphere','dog_cerebellum','dog_diencephalon','dog_midbrain','dog_olfactory_bulbs']:keys=['brain-lateral','brain-ventrolateral']
 elif id in ['dog_pons','dog_medulla']:keys=['brain-ventrolateral','brain-lateral']
 elif id in ['eye','dog_left_eye','dog_right_eye']:keys=['22-4','24-1']
 elif id in ['dog_head_skin','dog_head_fur','dog_skin_inner']:keys=['22-4','22-1']
 elif id in ['dog_frontalis','dog_malaris','dog_levator_anguli_oculi_medialis']:keys=['22-3','22-4']
 elif id in ['dog_orbicularis_oculi','dog_levator_nasolabialis','dog_levator_nasolabialis_cut','dog_zygomaticus']:keys=['22-1','22-2']
 elif id in ['dog_temporalis','dog_masseter']:keys=['23-3','23-10']
 elif id in ['dog_mandible','dog_zygomatic_temporal_process','dog_temporal_zygomatic_process']:keys=['23-3','23-6']
 elif id in ['dog_incisors','dog_canines','dog_premolars','dog_molars']:keys=['22-5','23-4']
 elif id in ['dog_occipital_bone','dog_occipital_bone_2','dog_parietal_bone']:keys=['22-9','23-10']
 else:keys=['22-12','22-9']
 # The frontal muscle gallery previously included an unrelated whole-brain plate.
 if id=='dog_frontalis':e['images']=[i for i in e['images'] if not i['url'].endswith('popesko-183.jpg')]
 for key in keys:
  if key.startswith('brain-'):
   name='web-umn-dog-'+key+'.jpg';title='Головной мозг собаки — '+('боковой вид' if key=='brain-lateral' else 'вентролатеральный вид')
   caption='Фото препарата мозга собаки: '+('полушарие, мозжечок и ствол.' if key=='brain-lateral' else 'ствол, мозжечок и область черепных нервов.')
   page='https://vanat.ahc.umn.edu/'+('grossbrain/Pages/HomePgs/ClickFirst.html' if key=='brain-lateral' else 'neurLab3/pages/MyelHndbrDgCranNN1229.html')
  else:
   name='web-umn-'+key+'.jpg';title,caption=titles[key];lab,n=key.split('-');page=f'https://vanat.ahc.umn.edu/carnLabs/Lab{lab}/Img{lab}-{n}.html'
  caption+=' University of Minnesota. Обзор анатомической области; граница выбранной детали модели отдельно не выделена.'
  if id in ['dog_left_eye','dog_right_eye','dog_left_hemisphere','dog_right_hemisphere']:caption+=' Сторона модели не выделена отдельно.'
  if id=='dog_head_fur':caption+=' Показаны веки и подлежащие ткани; микроструктура шерсти не показана.'
  if id=='dog_skin_inner':caption+=' Это ориентиры головы, а не идентификация технического фрагмента Skin.fbx.'
  e['images'].append(dict(title=title,caption=caption,url=PREFIX+name))
  extras[name]=dict(file=name,kind='internet-teaching-photograph',source_url=page,image_url=web[name],publisher='University of Minnesota',usage='Educational use; retain attribution. https://vanat.ahc.umn.edu/about.html')
 assert 3<=len(e['images'])<=6,id
 e.pop('imageUrls',None)
# Include every referenced file, preserve original bytes and original source manifests.
sources={s['file']:s for s in json.loads((ROOT/'output/element-atlas-full-upload/sources.json').read_text())};sources.update(extras)
needed={u.rsplit('/',1)[-1] for e in cat['elements'] for u in [e['iconUrl']]+[i['url'] for i in e['images']]}
for name in needed:
 dest=OUT/'images'/name
 if not dest.exists():shutil.copyfile(ROOT/'output/element-atlas-full-upload/images'/name,dest)
checks={};pixels={}
for name in sorted(needed):
 p=OUT/'images'/name
 with Image.open(p) as im:im.load();assert im.width>=300 and im.height>=177;pixels[name]=hashlib.sha256(im.convert('RGB').tobytes()).hexdigest()
 checks[name]=hashlib.sha256(p.read_bytes()).hexdigest()
assert all(name in sources for name in needed)
for e in cat['elements']:
 assert all(e.get(k) for k in ['id','name','nameLatin','shortDescription','fullDescription','iconUrl'])
 names=[i['url'].rsplit('/',1)[-1] for i in e['images']];assert len(set(names))==len(names);assert len({pixels[n] for n in names})==len(names)
 for i in e['images']:assert i['title'] and i['caption'] and i['url'].startswith(PREFIX)
sceneids=set()
for scene in ['SampleScene','PracticeScene']:
 sceneids.update(re.findall(r'(?m)^  _id: (.+)$',(ROOT/f'Assets/Scenes/{scene}.unity').read_text()))
ids=[e['id'] for e in cat['elements']];assert len(ids)==len(set(ids));assert not sceneids-set(ids),sceneids-set(ids)
write(OUT/'data/elements.example.json',cat)
write(OUT/'sources.json',[sources[n] for n in sorted(needed)])
write(OUT/'checksums.json',checks)
write(OUT/'element-images.json',[dict(id=e['id'],name=e['name'],nameLatin=e['nameLatin'],imageCount=len(e['images']),images=e['images']) for e in cat['elements']])
report=dict(version=13,elements=len(ids),sceneElementIds=len(sceneids),missingSceneIds=sorted(sceneids-set(ids)),galleryReferences=sum(len(e['images']) for e in cat['elements']),galleryCountRange=[min(len(e['images']) for e in cat['elements']),max(len(e['images']) for e in cat['elements'])],uniqueImageFiles=len(needed),newInternetPhotos=len(extras),allFilesDecode=True,distinctImagesWithinEachGallery=True,allLocalReferencesResolve=True)
write(OUT/'validation.json',report)
# Full browsing index: one row per element, its own images and Russian names.
import html
body=[]
for e in cat['elements']:
 figures=''.join(f'<figure><a href="images/{i["url"].rsplit("/",1)[-1]}"><img loading="lazy" src="images/{i["url"].rsplit("/",1)[-1]}"></a><figcaption>{html.escape(i["title"])}</figcaption></figure>' for i in e['images'])
 body.append(f'<section><h2>{html.escape(e["name"])}</h2><p>{html.escape(e["nameLatin"])} / {e["id"]} / {len(e["images"])} изображений</p><div>{figures}</div></section>')
(OUT/'gallery.html').write_text('<!doctype html><meta charset="utf-8"><title>Каталог иллюстраций</title><style>body{font:16px Arial;background:#f6f3ed;color:#17304a;margin:32px}section{border-bottom:1px solid #ccc;padding:20px 0}div{display:flex;gap:16px;flex-wrap:wrap}figure{margin:0;width:240px}img{width:240px;height:200px;object-fit:contain;background:white}figcaption{padding:8px}</style><h1>Финальный каталог v13</h1>'+''.join(body))
(OUT/'README.txt').write_text(f'''ФИНАЛЬНЫЙ НАБОР v13
{len(ids)} записей, от {report['galleryCountRange'][0]} до {report['galleryCountRange'][1]} разных изображений на элемент, {len(needed)} файлов.
Включены все {len(sceneids)} ID из сцен и дополнительная запись сердца. Четыре группы зубов включены; старые dog_teeth / dog_teeth_2 заменены.

ЗАГРУЗКА В SUPABASE (bucket element-content):
1. Содержимое images/ загрузить в images/ бакета (без дополнительной папки).
2. После картинок заменить data/elements.example.json файлом из архива.
3. Перезапустить приложение с интернетом.
URL каталога: https://zgmwmaustzdsqybamalu.supabase.co/storage/v1/object/public/element-content/data/elements.example.json
Сначала изображения, затем JSON. Старые серверные файлы можно оставить.

JSON использует существующие поля Unity: id, name, nameLatin, shortDescription, fullDescription, iconUrl, images[title,caption,url]. Модели в архив не входят.

ОБЗОР: открой gallery.html для просмотра картинок каждого элемента.
element-images.json — соответствия; sources.json — авторы, URL и страницы; checksums.json и validation.json — проверка. Эти файлы на сервер не нужны.

Атласы Попеско, МРТ-атласы и интернет-фотографии препаратов University of Minnesota. Иллюстрации одной анатомической области могут повторяться у разных элементов; внутри каждого элемента изображения различны. Общие виды подписаны как общие: они не выдают область за изолированную структуру и не отмечают выбранную сторону. Технический фрагмент skin_inner не имеет подтверждённой самостоятельной анатомической идентификации. Кошачьи препараты исключены; где источник не уточняет вид, это указано. Обозначения оригинальных таблиц сохранены. Источники сохранять для учебного использования.

Сервер этим действием не изменён. После загрузки серверных файлов новые данные подхватятся при следующей проверке.
''')
# Synchronize bundled catalogs only after all checks pass.
for relative in ['Assets/Content/elements.example.json','Assets/Resources/ElementContent/elements.json','Documentation/RemoteContent/elements.example.json']:write(ROOT/relative,cat)
archive=ROOT/'output/final-element-content-v13.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for name in ['README.txt','sources.json','checksums.json','element-images.json','validation.json','gallery.html','data/elements.example.json']:z.write(OUT/name,name)
 for name in sorted(needed):z.write(OUT/'images'/name,'images/'+name)
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
print(json.dumps(report,ensure_ascii=False,indent=2));print(archive,archive.stat().st_size)
