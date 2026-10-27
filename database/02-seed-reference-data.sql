-- Dados iniciais: listas pré-definidas (modelo de dados v02, secção 9; DA, F002 e F009).
-- Idempotente: pode ser executado mais do que uma vez sem duplicar linhas.

BEGIN;

INSERT INTO location (id, name, district, latitude, longitude) VALUES
    ('bada1dda-a0a5-57b9-9396-3985615b3b3f', 'Lisboa', 'Lisboa', 38.722252, -9.139337),
    ('c2bb4c43-c6e9-5751-af44-d407d97c16f1', 'Porto', 'Porto', 41.157944, -8.629105),
    ('bbaaacce-cacd-53d1-83e9-10f324c56a00', 'Vila Nova de Gaia', 'Porto', 41.123850, -8.611660),
    ('a588805f-08a9-5a29-92bf-dfac0cb2a957', 'Matosinhos', 'Porto', 41.182600, -8.689100),
    ('b2f082f3-fe7e-571c-976e-3e085d79e1d0', 'Maia', 'Porto', 41.235700, -8.619900),
    ('45d032ed-492f-5c10-925b-87a26908f1b9', 'Felgueiras', 'Porto', 41.364700, -8.197700),
    ('e2798a95-dbda-5979-902c-10124251722c', 'Penafiel', 'Porto', 41.208300, -8.283300),
    ('0ee69348-6e11-50ec-9e79-49fbe69ea6f0', 'Amarante', 'Porto', 41.270700, -8.082600),
    ('608dcc5b-1f1a-57d0-9526-5af6934927fc', 'Paços de Ferreira', 'Porto', 41.276400, -8.388900),
    ('7f878994-0879-5e38-97a3-fcbe7dcd73aa', 'Braga', 'Braga', 41.545400, -8.426500),
    ('536f002b-624c-580e-b3b7-470e69065937', 'Guimarães', 'Braga', 41.442600, -8.291800),
    ('645457dc-b02b-5900-96ab-445f21b6a565', 'Vila Nova de Famalicão', 'Braga', 41.407900, -8.519800),
    ('3a87e3b5-1cf4-5161-8c9f-79a89a0921d8', 'Viana do Castelo', 'Viana do Castelo', 41.693200, -8.832900),
    ('672b1ca0-3721-5b2f-bd2f-93c5aa7e0456', 'Aveiro', 'Aveiro', 40.640500, -8.653800),
    ('b85cd5c0-1b23-5e87-85f8-22e22e887f28', 'Coimbra', 'Coimbra', 40.203300, -8.410300),
    ('0cb922db-0b48-5b61-95fe-1aebe0a447a8', 'Leiria', 'Leiria', 39.743600, -8.807100),
    ('f577b67c-9afe-59ea-929d-465a5b6f3f45', 'Viseu', 'Viseu', 40.661000, -7.909700),
    ('d85fd474-2dd2-5c5d-8838-0e1bfaa15906', 'Vila Real', 'Vila Real', 41.300000, -7.744300),
    ('95403bb6-dfd8-5ec9-b7be-cca706c28d46', 'Bragança', 'Bragança', 41.806100, -6.756700),
    ('a9d21b96-20f2-5713-ba87-f21f8fd84f57', 'Guarda', 'Guarda', 40.537300, -7.267600),
    ('1b39cde1-862a-52cd-8939-91194b8213a0', 'Castelo Branco', 'Castelo Branco', 39.822200, -7.491500),
    ('51530da4-f909-5ca8-8a42-ec048a47aaba', 'Santarém', 'Santarém', 39.236200, -8.685900),
    ('ffbc7b27-af11-5b53-90b3-598535d0c2f4', 'Setúbal', 'Setúbal', 38.524400, -8.888200),
    ('02bd4f95-69ee-5c1e-9c9b-0ba36ea5f541', 'Évora', 'Évora', 38.571400, -7.907500),
    ('50464d05-d697-5c9f-98f1-3e536a1e942a', 'Beja', 'Beja', 38.015100, -7.863200),
    ('4a5fbc5e-3804-51db-b97f-ed6e3812dcfe', 'Faro', 'Faro', 37.019400, -7.932200),
    ('b3498910-1834-5c7b-943f-dd4a48bd2f01', 'Portalegre', 'Portalegre', 39.296700, -7.428500),
    ('a63ca9e7-680b-59fa-afe1-e2133aa63772', 'Sintra', 'Lisboa', 38.802900, -9.381700),
    ('44789030-ac1e-56c9-b5c7-11698ce06dc4', 'Oeiras', 'Lisboa', 38.697900, -9.314700),
    ('82ac4427-afc2-5c18-bcda-1f5bb7511c5c', 'Almada', 'Setúbal', 38.678900, -9.156500)
ON CONFLICT (name, district) DO NOTHING;

INSERT INTO skill (id, name) VALUES
    ('b3d323a1-902b-5305-81ba-5b0a7905364b', 'C#'),
    ('b7f599a6-22fa-52c0-9886-09cbd0f7b885', '.NET'),
    ('0d0a1697-d8d9-5705-8896-9dc6fc1866d9', 'ASP.NET Core'),
    ('4e992ebe-901d-5b24-8101-28d9e55ff0a6', 'Java'),
    ('d7ec63fd-fb72-53a2-aad7-998df3374be6', 'Spring Boot'),
    ('bf4ef088-5d58-5594-954c-ce634a7a31ab', 'Python'),
    ('651e7f6c-3c35-556f-a2dd-6d6057482f7a', 'JavaScript'),
    ('f79584d0-b603-5f93-883c-503eea256674', 'TypeScript'),
    ('8642415c-92ef-5d43-8273-6e09c46b1ac1', 'React'),
    ('56c24e29-7919-5c68-914a-897f8c79685f', 'Angular'),
    ('51a4feb1-6b19-5b84-a445-9a09980c9208', 'Flutter'),
    ('ee032def-1dcd-5240-90cb-7a8061d2d34a', 'Dart'),
    ('184f2378-8adc-5c65-853c-a7f05550dffc', 'SQL'),
    ('9e4890cb-a864-5f5b-9f0f-2a64f69b13a5', 'PostgreSQL'),
    ('359c23b6-6a19-528e-b6d3-955bd8f9940e', 'Git'),
    ('e0d1ff83-8597-5ec5-8295-f3eb31a1a0c5', 'Docker'),
    ('bb9a3357-442b-596c-99e1-0a75b2700dcb', 'Testes de software'),
    ('03bc9d07-6ef2-5f2e-8ecf-4bd13d2696c3', 'Análise de requisitos'),
    ('91fd4d8e-994b-5712-a2ff-05b5a2dfa370', 'Gestão de projetos'),
    ('5735e3c4-8ce6-56c3-98ef-416adba2b7f3', 'Atendimento ao cliente'),
    ('43b64a1a-bd52-5e41-bc7f-f5f91612106e', 'Vendas'),
    ('bcbb3814-e483-587b-8d71-e7ecb6ddca3e', 'Contabilidade'),
    ('688fb23f-0a73-5d03-93cc-ad476e5a5bd2', 'Excel avançado'),
    ('e5bcca6e-bdab-5191-949c-426f2953bcb9', 'Inglês'),
    ('cca05a1b-f455-5fbf-b4eb-017a0d2cc7e2', 'Espanhol'),
    ('9139773c-d022-51b8-bfa4-e6e166f44529', 'Condução de empilhadores'),
    ('2fc06881-46fe-5f2d-bb79-eb9791889502', 'Soldadura'),
    ('1ce1bd5d-0a44-5d0c-9119-963be1fbd6c6', 'Cozinha'),
    ('8d9d4242-f295-5ae3-9c0b-df378214e532', 'Logística'),
    ('5de95981-8dc8-5d08-bbd4-98f25f3770c0', 'Marketing digital')
ON CONFLICT ((lower(name))) DO NOTHING;

INSERT INTO benefit (id, name) VALUES
    ('d0d61d37-3afa-5f34-ba6e-4739a1b9886a', 'Seguro de saúde'),
    ('52f93e51-2a12-55c2-bbbd-7369ed2b6737', 'Subsídio de alimentação'),
    ('6fafb74d-6d0c-5068-9743-419584b77e0c', 'Horário flexível'),
    ('d1f17174-9c26-56c1-b455-b88d7f42e4d0', 'Teletrabalho'),
    ('6da65bf1-4f51-527a-86f2-50886a06df50', 'Formação paga'),
    ('208774b6-9078-5520-8210-1148a9d0287c', 'Prémios de desempenho'),
    ('b5a4ea89-da80-569b-96a9-3008884d18f8', 'Viatura da empresa'),
    ('6e800ab6-18b9-5d97-af52-2de2f6fbd4c4', 'Telemóvel da empresa'),
    ('324661a4-0dbd-52d5-8b32-bb0235f5c43b', 'Dia de aniversário livre'),
    ('0ccd91de-e0b0-57b8-8e53-de633bb02260', 'Ginásio'),
    ('3c4d9949-240b-54cc-bfd4-b16b22cadb5b', 'Creche ou apoio à infância'),
    ('ec6528be-9a6b-5499-a899-d2853529dd7d', 'Plano de carreira')
ON CONFLICT ((lower(name))) DO NOTHING;

COMMIT;
