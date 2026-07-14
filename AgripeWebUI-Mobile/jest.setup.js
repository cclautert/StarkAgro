// Ao ser importado, o axios sonda qual adapter usar. Essa sondagem
// (axios/lib/adapters/fetch.js) cria um ReadableStream, prende um reader nele e chama
// .cancel(). O polyfill de stream do Expo estoura nesse caso — "Cannot cancel a stream
// that already has a reader" — e, como o erro acontece na CARGA do módulo, o simples
// `import axios` derrubava a suíte inteira antes de rodar um único teste. Eram 5 suítes
// (todas as de services) sempre vermelhas.
//
// Aqui devolvemos ao ambiente de teste o ReadableStream do próprio Node, que segue a spec
// (cancelar um stream travado devolve promise rejeitada, não explode). Só vale nos testes:
// no app de verdade quem roda é o polyfill do Expo, que não tocamos.
const { ReadableStream, WritableStream, TransformStream } = require('node:stream/web');

global.ReadableStream = ReadableStream;
global.WritableStream = WritableStream;
global.TransformStream = TransformStream;
