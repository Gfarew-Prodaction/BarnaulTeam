"""Package the existing atlas images and Unity-exported element plates for Supabase."""
import copy
import json
import shutil
import zipfile
from pathlib import Path
from urllib.parse import urlsplit, unquote

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/dog-head-complete-upload'
PREFIX = 'https://zgmwmaustzdsqybamalu.supabase.co/storage/v1/object/public/element-content/images/'
CATALOGS = [ROOT / p for p in ('Assets/Content/elements.example.json', 'Assets/Resources/ElementContent/elements.json', 'Documentation/RemoteContent/elements.example.json')]

def read(path):
    return json.loads(path.read_text())

def write(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + '\n')

def main():
    catalog = read(CATALOGS[0])
    brain = {Path(i['url']).name: i for i in read(ROOT / 'output/atlas-top10/images-for-json.json')}
    eye = read(ROOT / 'output/eye-upload/images-for-json.json')
    mappings = {
        'dog_pons': ['04_cerebellum-pons.jpg', '08_head-mri-longitudinal.jpg'],
        'dog_cerebellum': ['04_cerebellum-pons.jpg', '08_head-mri-longitudinal.jpg'],
        'dog_diencephalon': ['02_diencephalon.jpg', '07_brain-mri-central.jpg'],
        'dog_midbrain': ['03_midbrain.jpg', '08_head-mri-longitudinal.jpg'],
        'dog_medulla': ['05_medulla-oblongata.jpg', '08_head-mri-longitudinal.jpg'],
        'dog_left_hemisphere': ['01_cerebral-hemispheres.jpg', '07_brain-mri-central.jpg'],
        'dog_right_hemisphere': ['01_cerebral-hemispheres.jpg', '07_brain-mri-central.jpg'],
        'dog_olfactory_bulbs': ['06_brain-mri-rostral.jpg', '08_head-mri-longitudinal.jpg'],
    }
    manifest = []
    for element in catalog['elements']:
        key = element['id']
        if not key.startswith('dog_'):
            continue
        filename = key + '-model.jpg'
        assert (OUT / 'images' / filename).is_file(), filename
        atlas = [i for i in element.get('images', []) if i.get('url') and not i['url'].endswith('-model.jpg')]
        if key in mappings:
            atlas = [copy.deepcopy(brain[name]) for name in mappings[key]]
            for image in atlas:
                if key == 'dog_olfactory_bulbs':
                    image['caption'] += ' Обзорный срез головы/мозга; луковицы отдельно не выделены.'
                if key.endswith('_hemisphere'):
                    image['caption'] += ' Обзор обоих полушарий; выбранная сторона отдельно не выделена.'
        if key in ('dog_head_skin', 'dog_head_fur', 'dog_skin_inner'):
            atlas = [copy.deepcopy(eye[0])]
            atlas[0]['title'] = 'Области головы собаки'
            atlas[0]['caption'] = 'Обзорная схема областей головы. Этот фрагмент покрова отдельно не выделен.'
        if key in ('dog_left_eye', 'dog_right_eye'):
            atlas = [copy.deepcopy(eye[i]) for i in (0, 2, 4)]
            for image in atlas:
                image['caption'] += ' Обзорная иллюстрация; сторона выбранного глаза не выделена.'
        element['images'] = [{
            'title': element['name'],
            'caption': 'Учебная 3D-визуализация элемента из модели проекта в двух ракурсах. ' + (
                'Парные части с одинаковым ID показаны вместе.' if key in ('dog_frontalis', 'dog_levator_nasolabialis', 'dog_masseter', 'dog_levator_anguli_oculi_medialis', 'dog_orbicularis_oculi', 'dog_temporalis', 'dog_malaris', 'dog_zygomaticus', 'dog_olfactory_bulbs') else
                'Границы фрагмента соответствуют исходному файлу модели.'),
            'url': PREFIX + filename
        }] + atlas
        if not element.get('iconUrl'):
            element['iconUrl'] = PREFIX + filename
        manifest.append({'id': key, 'name': element['name'], 'iconUrl': element['iconUrl'], 'images': element['images']})

    search_dirs = [ROOT / 'output/dog-head-upload/images', ROOT / 'output/atlas-top10/images', ROOT / 'output/eye-upload/images']
    required = set()
    for e in catalog['elements']:
        for url in [e.get('iconUrl', '')] + [i.get('url', '') for i in e.get('images', [])]:
            if url:
                assert url.startswith(PREFIX), url
                required.add(unquote(urlsplit(url).path.rsplit('/', 1)[-1]))
    for name in sorted(required):
        dst = OUT / 'images' / name
        if dst.is_file():
            continue
        src = next((directory / name for directory in search_dirs if (directory / name).is_file()), None)
        assert src is not None, f'Missing local image: {name}'
        shutil.copyfile(src, dst)

    catalog['version'] = max(10, catalog['version'])
    write(OUT / 'data/elements.example.json', catalog)
    write(OUT / 'element-images.json', manifest)
    sources = []
    for folder in ('dog-head-upload', 'atlas-top10', 'eye-upload'):
        sources.append({'package': folder, 'atlas_sources': read(ROOT / f'output/{folder}/sources.json')})
    sources.append({'kind': '3D model rendering', 'files': [m['id'] + '-model.jpg' for m in manifest], 'scene': 'Assets/Scenes/SampleScene.unity', 'source_models': ['skull_muscles_textured123.fbx', 'Brain228.fbx', 'Skin.fbx'], 'note': 'Два ракурса одной модели; не фотография и не независимая анатомическая схема. У Skin отсутствуют исходные текстуры. Уши объединены с кожей.'})
    write(OUT / 'sources.json', sources)
    (OUT / 'README.txt').write_text('''Картинки для всех 34 анатомических ID головы собаки (43 объекта сцены).

1. Supabase → Storage → element-content → images: загрузить ВСЕ файлы из images/ архива, без дополнительной вложенной папки. При совпадении имён разрешить замену.
2. Затем загрузить data/elements.example.json в element-content/data/elements.example.json с заменой прежнего JSON.
3. Полностью перезапустить приложение с интернетом: оно проверит JSON и скачает новые картинки. Версия каталога: 10.

У каждого ID — собственная иллюстрация модели в двух ракурсах и подходящие обзорные изображения из уже использованных атласов. Парные части одной мышцы имеют общий ID и показаны вместе. Для мозга срезы подобраны по отделу; обзорные схемы не выданы за выделенную структуру. У существующих элементов иконки сохранены; для кожи без прежней иконки использовано изображение модели.

element-images.json — соответствия ID и изображений; sources.json — происхождение иллюстраций. Эти два файла и preview.jpg загружать на сервер не требуется. Исходные FBX и PDF не входят в архив.
''')
    # Validate complete archive before changing the three project catalogs.
    from PIL import Image
    for name in required:
        with Image.open(OUT / 'images' / name) as image:
            assert image.width > 0 and image.height > 0
            image.verify()
    assert len(manifest) == 34
    assert all(len(e['images']) >= 2 and e['iconUrl'] for e in manifest)
    for path in CATALOGS:
        write(path, catalog)
    archive = ROOT / 'output/dog-head-complete-upload.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        for path in sorted(OUT.rglob('*')):
            if path.is_file() and not path.name.startswith('.'):
                z.write(path, path.relative_to(OUT))
    print(json.dumps({'ids': len(manifest), 'images': len(required), 'archive': str(archive), 'bytes': archive.stat().st_size}, ensure_ascii=False))

if __name__ == '__main__':
    main()
